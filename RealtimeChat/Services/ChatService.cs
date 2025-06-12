using Microsoft.EntityFrameworkCore;
using RealtimeChat.Data;
using RealtimeChat.Models;
using Microsoft.AspNetCore.SignalR;
using RealtimeChat.Hubs;

namespace RealtimeChat.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatService(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<List<ChatRoom>> GetPublicRoomsAsync()
        {
            return await _context.ChatRooms
                .Where(r => !r.IsPrivate)
                .Include(r => r.Members)
                .ThenInclude(m => m.User)
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<List<ChatRoom>> GetUserRoomsAsync(string userId)
        {
            // Fixed: Simplified logic - get rooms where user is a member
            return await _context.ChatRoomMembers
                .Where(crm => crm.UserId == userId)
                .Include(crm => crm.ChatRoom)
                .ThenInclude(cr => cr.Members)
                .ThenInclude(m => m.User)
                .Select(crm => crm.ChatRoom)
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<ChatRoom> GetRoomAsync(int roomId)
        {
            return await _context.ChatRooms
                .Include(r => r.Members) // Eager load Members
                .ThenInclude(m => m.User) // Eager load User details for each member
                .FirstOrDefaultAsync(r => r.Id == roomId) ?? throw new Exception("Room not found");
        }

        public async Task<List<Message>> GetRoomMessagesAsync(int roomId, int page = 1, int pageSize = 50)
        {
            return await _context.Messages
                .Where(m => m.ChatRoomId == roomId)
                .Include(m => m.Sender)
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<List<Message>> GetPrivateMessagesAsync(string userId1, string userId2, int page = 1, int pageSize = 50)
        {
            return await _context.Messages
                .Where(m => m.IsPrivate &&
                           ((m.SenderId == userId1 && m.RecipientId == userId2) ||
                            (m.SenderId == userId2 && m.RecipientId == userId1)))
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<ChatRoom> CreateRoomAsync(string name, string? description, bool isPrivate, string createdById)
        {
            var room = new ChatRoom
            {
                Name = name,
                Description = description,
                IsPrivate = isPrivate,
                CreatedById = createdById
            };

            _context.ChatRooms.Add(room);
            await _context.SaveChangesAsync();

            // Add creator as admin member
            var membership = new ChatRoomMember
            {
                UserId = createdById,
                ChatRoomId = room.Id,
                IsAdmin = true
            };

            _context.ChatRoomMembers.Add(membership);
            await _context.SaveChangesAsync();

            // ADDED: Notify about new member (creator) joining
            var user = await _context.Users.FindAsync(createdById);
            if (user != null)
            {
                await _hubContext.Clients.Group($"Room_{room.Id}")
                    .SendAsync("MemberJoinedRoom", user.UserName, user.DisplayName, room.Id);
            }

            return room;
        }

        public async Task<bool> JoinPublicRoomAsync(int roomId, string userId)
        {
            // Check if room exists and is public
            var room = await _context.ChatRooms.FindAsync(roomId);
            if (room == null || room.IsPrivate)
                return false;

            // Check if user is already a member
            var existingMembership = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(crm => crm.ChatRoomId == roomId && crm.UserId == userId);

            if (existingMembership != null)
                return false;

            var membership = new ChatRoomMember
            {
                UserId = userId,
                ChatRoomId = roomId,
                IsAdmin = false
            };

            _context.ChatRoomMembers.Add(membership);
            await _context.SaveChangesAsync();

            // ADDED: Notify all room members about new member joining
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                await _hubContext.Clients.Group($"Room_{roomId}")
                    .SendAsync("MemberJoinedRoom", user.UserName, user.DisplayName, roomId);
            }

            return true;
        }

        public async Task<bool> InviteToRoomAsync(int roomId, string inviterId, string inviteeId)
        {
            // Check if inviter is admin of the room
            if (!await IsRoomAdminAsync(roomId, inviterId))
                return false;

            // Check if invitee is already a member
            if (await IsRoomMemberAsync(roomId, inviteeId))
                return false;

            // Check if there's already a pending invitation
            var existingInvitation = await _context.RoomInvitations
                .FirstOrDefaultAsync(ri => ri.ChatRoomId == roomId &&
                                          ri.InviteeId == inviteeId &&
                                          ri.Status == InvitationStatus.Pending);

            if (existingInvitation != null)
                return false;

            var invitation = new RoomInvitation
            {
                ChatRoomId = roomId,
                InviterId = inviterId,
                InviteeId = inviteeId,
                Status = InvitationStatus.Pending
            };

            _context.RoomInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            // ADDED: Send SignalR notification to the invitee
            var fullInvitation = await GetInvitationDetailsAsync(invitation.Id);
            if (fullInvitation != null)
            {
                await _hubContext.Clients.User(inviteeId)
                    .SendAsync("ReceiveRoomInvitation", fullInvitation);
            }

            return true;
        }

        public async Task<List<RoomInvitation>> GetPendingInvitationsAsync(string userId)
        {
            return await _context.RoomInvitations
                .Where(ri => ri.InviteeId == userId && ri.Status == InvitationStatus.Pending)
                .Include(ri => ri.ChatRoom)
                .Include(ri => ri.Inviter)
                .OrderByDescending(ri => ri.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> AcceptInvitationAsync(int invitationId, string userId)
        {
            var invitation = await _context.RoomInvitations
                .FirstOrDefaultAsync(ri => ri.Id == invitationId &&
                                          ri.InviteeId == userId &&
                                          ri.Status == InvitationStatus.Pending);

            if (invitation == null)
                return false;

            // Add user to room
            var membership = new ChatRoomMember
            {
                UserId = userId,
                ChatRoomId = invitation.ChatRoomId,
                IsAdmin = false
            };

            _context.ChatRoomMembers.Add(membership);

            // Update invitation status
            invitation.Status = InvitationStatus.Accepted;
            invitation.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // ADDED: Notify all room members about new member joining
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                await _hubContext.Clients.Group($"Room_{invitation.ChatRoomId}")
                    .SendAsync("MemberJoinedRoom", user.UserName, user.DisplayName, invitation.ChatRoomId);
            }

            return true;
        }

        public async Task<bool> DeclineInvitationAsync(int invitationId, string userId)
        {
            var invitation = await _context.RoomInvitations
                .FirstOrDefaultAsync(ri => ri.Id == invitationId &&
                                          ri.InviteeId == userId &&
                                          ri.Status == InvitationStatus.Pending);

            if (invitation == null)
                return false;

            // Update invitation status
            invitation.Status = InvitationStatus.Declined;
            invitation.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<Message> SendMessageAsync(string senderId, int? chatRoomId, string? recipientId, string content)
        {
            var message = new Message
            {
                SenderId = senderId,
                ChatRoomId = chatRoomId,
                RecipientId = recipientId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsPrivate = recipientId != null
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Load sender details for SignalR
            message = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstAsync(m => m.Id == message.Id);

            // Send SignalR notification
            if (message.IsPrivate)
            {
                // Send to both sender and recipient for private messages
                await _hubContext.Clients.Users(new[] { senderId, recipientId! })
                    .SendAsync("ReceivePrivateMessage", message);
            }
            else if (chatRoomId.HasValue)
            {
                // Send to all users in the room
                await _hubContext.Clients.Group($"Room_{chatRoomId}")
                    .SendAsync("ReceiveMessage", message);
            }

            return message;
        }

        public async Task<bool> LeaveRoomAsync(int roomId, string userId)
        {
            var membership = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(crm => crm.ChatRoomId == roomId && crm.UserId == userId);

            if (membership == null)
                return false;

            _context.ChatRoomMembers.Remove(membership);
            await _context.SaveChangesAsync();

            // Notify all room members about user leaving
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                await _hubContext.Clients.Group($"Room_{roomId}")
                    .SendAsync("MemberLeftRoom", user.UserName, user.DisplayName, roomId);
            }

            return true;
        }

        public async Task<bool> IsRoomMemberAsync(int roomId, string userId)
        {
            return await _context.ChatRoomMembers
                .AnyAsync(crm => crm.ChatRoomId == roomId && crm.UserId == userId);
        }

        public async Task<bool> IsRoomAdminAsync(int roomId, string userId)
        {
            return await _context.ChatRoomMembers
                .AnyAsync(crm => crm.ChatRoomId == roomId && crm.UserId == userId && crm.IsAdmin);
        }

        public async Task<RoomInvitation> GetInvitationDetailsAsync(int invitationId)
        {
            return await _context.RoomInvitations
                .Include(ri => ri.ChatRoom)
                .Include(ri => ri.Inviter)
                .Include(ri => ri.Invitee)
                .FirstOrDefaultAsync(ri => ri.Id == invitationId) ?? throw new Exception("Invitation not found");
        }

        public async Task<List<ApplicationUser>> GetRoomMembersAsync(int roomId)
        {
            return await _context.ChatRoomMembers
                .Where(crm => crm.ChatRoomId == roomId)
                .Include(crm => crm.User)
                .Select(crm => crm.User)
                .OrderBy(u => u.DisplayName)
                .ToListAsync();
        }

        public async Task<List<ApplicationUser>> GetOnlineUsersAsync()
        {
            // This would typically track online status via SignalR connections
            // For now, return all users - you'd need to implement connection tracking
            return await _context.Users
                .Where(u => u.IsOnline) // Assuming you have an IsOnline property
                .OrderBy(u => u.DisplayName)
                .ToListAsync();
        }

        public async Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<ApplicationUser>();

            var lowerSearchTerm = searchTerm.ToLower();
            return await _context.Users
                .Where(u => u.UserName.ToLower().Contains(lowerSearchTerm) ||
                           u.DisplayName.ToLower().Contains(lowerSearchTerm) ||
                           (u.Email != null && u.Email.ToLower().Contains(lowerSearchTerm)))
                .OrderBy(u => u.DisplayName)
                .Take(20) // Limit results
                .ToListAsync();
        }

        public async Task<List<ChatRoom>> SearchRoomsAsync(string searchTerm, string? userId = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<ChatRoom>();

            var lowerSearchTerm = searchTerm.ToLower();

            // Build the base query with all conditions first
            var baseQuery = _context.ChatRooms
                .Where(r => //!r.IsPrivate &&  Only search public rooms
                           (r.Name.ToLower().Contains(lowerSearchTerm) ||
                            (r.Description != null && r.Description.ToLower().Contains(lowerSearchTerm))));

            // If userId is provided, exclude rooms the user is already in
            //if (!string.IsNullOrEmpty(userId))
            //{
            //    var userRoomIds = await _context.ChatRoomMembers
            //        .Where(crm => crm.UserId == userId)
            //        .Select(crm => crm.ChatRoomId)
            //        .ToListAsync();

            //    baseQuery = baseQuery.Where(r => !userRoomIds.Contains(r.Id));
            //}

            // Apply includes, ordering and limit at the end
            return await baseQuery
                .Include(r => r.Members)
                .ThenInclude(m => m.User)
                .OrderBy(r => r.Name)
                .Take(20) // Limit results
                .ToListAsync();
        }


    }
}