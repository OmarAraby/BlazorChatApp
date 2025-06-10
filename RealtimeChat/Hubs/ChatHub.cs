using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Data;
using RealtimeChat.Dtos;
using RealtimeChat.Models;
using RealtimeChat.Services;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

            if (userId == null) return;

            // Allow joining public rooms directly
            var room = await _chatService.GetRoomAsync(roomId);
            if (room != null && !room.IsPrivate && !await _chatService.IsRoomMemberAsync(roomId, userId))
            {
                if (await _chatService.JoinPublicRoomAsync(roomId, userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
                    await Clients.Group($"Room_{roomId}").SendAsync("UserJoinedRoom", new { UserId = userId, RoomId = roomId, UserName = Context.User?.Identity?.Name });
                    return;
                }
            }

            // For private rooms or existing members
            if (await _chatService.IsRoomMemberAsync(roomId, userId))
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

            if (inviterId == null) return;

            if (await _chatService.InviteToRoomAsync(roomId, inviterId, inviteeId))
            {
                var invitation = await _context.RoomInvitations
                    .Include(ri => ri.ChatRoom)
                    .Include(ri => ri.Inviter)
                    .FirstOrDefaultAsync(ri => ri.ChatRoomId == roomId && ri.InviteeId == inviteeId && ri.Status == InvitationStatus.Pending);

                if (invitation != null)
                {
                    Console.WriteLine($"Sending invitation to {inviteeId}, Invitation ID: {invitation.Id}");
                    await Clients.User(inviteeId).SendAsync("ReceiveRoomInvitation", invitation);
                }

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

            if (userId == null) return;

            if (await _chatService.AcceptInvitationAsync(invitationId, userId))
            {
                var acceptedInvitation = await _context.RoomInvitations
                    .Include(ri => ri.ChatRoom)
                    .FirstOrDefaultAsync(ri => ri.Id == invitationId);

                if (acceptedInvitation != null)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{acceptedInvitation.ChatRoomId}");
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

            if (userId == null) return;

            if (await _chatService.DeclineInvitationAsync(invitationId, userId))
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

            if (userId == null) return;

            if (await _chatService.LeaveRoomAsync(roomId, userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomId}");
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