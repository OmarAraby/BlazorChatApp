using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Data;
using RealtimeChat.Dtos;
using RealtimeChat.Models;
using System.Text.RegularExpressions;

namespace RealtimeChat.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task SendMessageToRoom(int roomId, string message)
        {
            var userId = Context.UserIdentifier;
            var user = await _userManager.FindByIdAsync(userId!);

            if (user == null) return;

            var isMember = await _context.ChatRoomMembers
                .AnyAsync(crm => crm.UserId == userId && crm.ChatRoomId == roomId);

            if (!isMember) return;

            var chatMessage = new Message
            {
                Content = message,
                SenderId = userId!,
                ChatRoomId = roomId,
                SentAt = DateTime.UtcNow,
                IsPrivate = false
            };

            _context.Messages.Add(chatMessage);
            await _context.SaveChangesAsync();

            var messageDto = new MessageDto
            {
                Id = chatMessage.Id,
                Content = chatMessage.Content,
                SenderId = chatMessage.SenderId,
                SenderName = user.DisplayName ?? user.Email!,
                SentAt = chatMessage.SentAt,
                RoomId = roomId
            };

            await Clients.Group($"Room_{roomId}").SendAsync("ReceiveRoomMessage", messageDto);
        }
        public async Task SendPrivateMessage(string recipientId, string message)
        {
            var senderId = Context.UserIdentifier;
            var sender = await _userManager.FindByIdAsync(senderId!);
            var recipient = await _userManager.FindByIdAsync(recipientId);

            if (sender == null || recipient == null) return;

            var chatMessage = new Message
            {
                Content = message,
                SenderId = senderId!,
                RecipientId = recipientId,
                SentAt = DateTime.UtcNow,
                IsPrivate = true
            };

            _context.Messages.Add(chatMessage);
            await _context.SaveChangesAsync();

            var messageData = new
            {
                Id = chatMessage.Id,
                Content = chatMessage.Content,
                SenderName = sender.DisplayName ?? sender.UserName,
                SenderId = senderId,
                RecipientId = recipientId,
                SentAt = chatMessage.SentAt
            };

            await Clients.User(recipientId).SendAsync("ReceivePrivateMessage", messageData);
            await Clients.User(senderId!).SendAsync("ReceivePrivateMessage", messageData);
        }

        public async Task JoinRoom(int roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
        }

        public async Task LeaveRoom(int roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomId}");
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.IsOnline = true;
                    user.LastSeen = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }
                Console.WriteLine($"User {Context.UserIdentifier} connected");
                await Clients.All.SendAsync("UserOnline", userId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.IsOnline = false;
                    user.LastSeen = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }

                await Clients.All.SendAsync("UserOffline", userId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
