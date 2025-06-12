using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Data;
using RealtimeChat.Dtos;
using RealtimeChat.Models;
using RealtimeChat.Services;

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

            if (user == null || !await _chatService.IsRoomMemberAsync(roomId, userId!))
            {
                await Clients.Caller.SendAsync("Error", "User not authorized or not a room member.");
                return;
            }

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

            if (sender == null || recipient == null)
            {
                await Clients.Caller.SendAsync("Error", "Invalid sender or recipient.");
                return;
            }

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

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                await Clients.Caller.SendAsync("Error", "User not found.");
                return;
            }

            var room = await _chatService.GetRoomAsync(roomId);
            if (room == null)
            {
                await Clients.Caller.SendAsync("Error", "Room not found.");
                return;
            }

            if (!room.IsPrivate && !await _chatService.IsRoomMemberAsync(roomId, userId))
            {
                if (await _chatService.JoinPublicRoomAsync(roomId, userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
                    await Clients.Group($"Room_{roomId}").SendAsync("UserJoinedRoom", new
                    {
                        UserId = userId,
                        RoomId = roomId,
                        //UserName = user.UserName,
                        DisplayName = user.DisplayName ?? user.UserName
                    });
                    await Clients.Group($"Room_{roomId}").SendAsync("MemberJoinedRoom", user.UserName, user.DisplayName ?? user.UserName, roomId);
                    return;
                }
            }

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
            if (inviterId == null)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated.");
                return;
            }

            try
            {
                if (await _chatService.InviteToRoomAsync(roomId, inviterId, inviteeId))
                {
                    var invitation = await _context.RoomInvitations
                        .Include(ri => ri.ChatRoom)
                        .Include(ri => ri.Inviter)
                        .FirstOrDefaultAsync(ri => ri.ChatRoomId == roomId && ri.InviteeId == inviteeId && ri.Status == InvitationStatus.Pending);

                    if (invitation != null)
                    {
                        Console.WriteLine($"Sending invitation to {inviteeId}, Invitation ID: {invitation.Id}, Room: {invitation.ChatRoom?.Name}");
                        await Clients.User(inviteeId).SendAsync("ReceiveRoomInvitation", invitation);
                    }

                    await Clients.Caller.SendAsync("InvitationSent", new { Success = true, RoomId = roomId });
                }
                else
                {
                    Console.WriteLine($"Failed to send invitation to {inviteeId} for room {roomId}");
                    await Clients.Caller.SendAsync("InvitationSent", new { Success = false, RoomId = roomId, Message = "Failed to send invitation." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending invitation: {ex.Message}");
                await Clients.Caller.SendAsync("InvitationSent", new { Success = false, RoomId = roomId, Message = ex.Message });
            }
        }

        public async Task AcceptRoomInvitation(int invitationId)
        {
            var userId = Context.UserIdentifier;
            if (userId == null)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated.");
                return;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                await Clients.Caller.SendAsync("Error", "User not found.");
                return;
            }

            try
            {
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
                                UserName = user.UserName,
                                DisplayName = user.DisplayName ?? user.UserName
                            });
                        await Clients.Group($"Room_{acceptedInvitation.ChatRoomId}")
                            .SendAsync("MemberJoinedRoom", user.UserName, user.DisplayName ?? user.UserName, acceptedInvitation.ChatRoomId);
                    }

                    await Clients.Caller.SendAsync("InvitationAccepted", new { Success = true, InvitationId = invitationId });
                }
                else
                {
                    await Clients.Caller.SendAsync("InvitationAccepted", new { Success = false, InvitationId = invitationId, Message = "Failed to accept invitation." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting invitation {invitationId}: {ex.Message}");
                await Clients.Caller.SendAsync("InvitationAccepted", new { Success = false, InvitationId = invitationId, Message = ex.Message });
            }
        }

        public async Task DeclineRoomInvitation(int invitationId)
        {
            var userId = Context.UserIdentifier;
            if (userId == null)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated.");
                return;
            }

            try
            {
                if (await _chatService.DeclineInvitationAsync(invitationId, userId))
                {
                    await Clients.Caller.SendAsync("InvitationDeclined", new { Success = true, InvitationId = invitationId });
                }
                else
                {
                    await Clients.Caller.SendAsync("InvitationDeclined", new { Success = false, InvitationId = invitationId, Message = "Failed to decline invitation." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error declining invitation {invitationId}: {ex.Message}");
                await Clients.Caller.SendAsync("InvitationDeclined", new { Success = false, InvitationId = invitationId, Message = ex.Message });
            }
        }

        public async Task LeaveRoomPermanently(int roomId)
        {
            var userId = Context.UserIdentifier;
            if (userId == null)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated.");
                return;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                await Clients.Caller.SendAsync("Error", "User not found.");
                return;
            }

            try
            {
                if (await _chatService.LeaveRoomAsync(roomId, userId))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomId}");
                    await Clients.Group($"Room_{roomId}")
                        .SendAsync("UserLeftRoom", new
                        {
                            UserId = userId,
                            RoomId = roomId,
                            //UserName = user.UserName,
                            DisplayName = user.DisplayName ?? user.UserName
                        });
                    // Add MemberLeftRoom notification
                    await Clients.Group($"Room_{roomId}")
                        .SendAsync("MemberLeftRoom", user.UserName, user.DisplayName ?? user.UserName, roomId);
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error leaving room {roomId}: {ex.Message}");
                await Clients.Caller.SendAsync("LeftRoom", new { Success = false, RoomId = roomId, Message = ex.Message });
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

                    Console.WriteLine($"User {userId} connected");
                }

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

                Console.WriteLine($"User {userId} disconnected");
                await Clients.All.SendAsync("UserOffline", userId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}