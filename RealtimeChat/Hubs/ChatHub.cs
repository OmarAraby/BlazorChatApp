using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Data;
using RealtimeChat.Dtos;
using RealtimeChat.Models;
using RealtimeChat.Services;
using System.Text.RegularExpressions;

namespace RealtimeChat.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IChatService _chatService;


        public ChatHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IChatService chatService)
        {
            _context = context;
            _userManager = userManager;
            _chatService = chatService;
        }

        public async Task SendMessageToRoom(int roomId, string message)
        {
            var userId = Context.UserIdentifier;
            var user = await _userManager.FindByIdAsync(userId!);

            if (user == null) return;

            // Check if user is a member of the room
            if (!await _chatService.IsRoomMemberAsync(roomId, userId!))
                return;
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
            var userId = Context.UserIdentifier;

            // Verify user is a member of the room
            if (await _chatService.IsRoomMemberAsync(roomId, userId!))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
            }
        }

        public async Task LeaveRoom(int roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomId}");
        }

        public async Task InviteUserToRoom(int roomId, string inviteeId)
        {
            var inviterId = Context.UserIdentifier;

            if (await _chatService.InviteToRoomAsync(roomId, inviterId!, inviteeId))
            {
                var room = await _chatService.GetRoomAsync(roomId);
                var inviter = await _userManager.FindByIdAsync(inviterId!);

                // Notify the invitee
                await Clients.User(inviteeId).SendAsync("ReceiveRoomInvitation", new
                {
                    RoomId = roomId,
                    RoomName = room.Name,
                    InviterName = inviter?.DisplayName ?? inviter?.UserName,
                    InviterId = inviterId
                });

                // Notify the inviter of success
                await Clients.Caller.SendAsync("InvitationSent", new { Success = true, RoomId = roomId });
            }
            else
            {
                await Clients.Caller.SendAsync("InvitationSent", new { Success = false, RoomId = roomId });
            }
        }

        public async Task AcceptRoomInvitation(int invitationId)
        {
            var userId = Context.UserIdentifier;

            if (await _chatService.AcceptInvitationAsync(invitationId, userId!))
            {
                // Get the room details to join the SignalR group
                var invitations = await _chatService.GetPendingInvitationsAsync(userId!);
                var acceptedInvitation = await _context.RoomInvitations
                    .Include(ri => ri.ChatRoom)
                    .FirstOrDefaultAsync(ri => ri.Id == invitationId);

                if (acceptedInvitation != null)
                {
                    // Join the room group
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{acceptedInvitation.ChatRoomId}");

                    // Notify room members of new member
                    await Clients.Group($"Room_{acceptedInvitation.ChatRoomId}")
                        .SendAsync("UserJoinedRoom", new
                        {
                            UserId = userId,
                            RoomId = acceptedInvitation.ChatRoomId,
                            UserName = Context.User?.Identity?.Name
                        });
                }

                await Clients.Caller.SendAsync("InvitationAccepted", new { Success = true, InvitationId = invitationId });
            }
            else
            {
                await Clients.Caller.SendAsync("InvitationAccepted", new { Success = false, InvitationId = invitationId });
            }
        }

        public async Task DeclineRoomInvitation(int invitationId)
        {
            var userId = Context.UserIdentifier;

            if (await _chatService.DeclineInvitationAsync(invitationId, userId!))
            {
                await Clients.Caller.SendAsync("InvitationDeclined", new { Success = true, InvitationId = invitationId });
            }
            else
            {
                await Clients.Caller.SendAsync("InvitationDeclined", new { Success = false, InvitationId = invitationId });
            }
        }

        public async Task LeaveRoomPermanently(int roomId)
        {
            var userId = Context.UserIdentifier;

            if (await _chatService.LeaveRoomAsync(roomId, userId!))
            {
                // Remove from SignalR group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomId}");

                // Notify remaining room members
                await Clients.Group($"Room_{roomId}")
                    .SendAsync("UserLeftRoom", new
                    {
                        UserId = userId,
                        RoomId = roomId,
                        UserName = Context.User?.Identity?.Name
                    });

                await Clients.Caller.SendAsync("LeftRoom", new { Success = true, RoomId = roomId });
            }
            else
            {
                await Clients.Caller.SendAsync("LeftRoom", new
                {
                    Success = false,
                    RoomId = roomId,
                    Message = "Cannot leave room. You may be the only admin."
                });
            }
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

                    // Join all rooms the user is a member of
                    var userRooms = await _chatService.GetUserRoomsAsync(userId);
                    foreach (var room in userRooms)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{room.Id}");
                    }
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
