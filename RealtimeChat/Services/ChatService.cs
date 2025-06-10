using Microsoft.EntityFrameworkCore;
using RealtimeChat.Data;
using RealtimeChat.Models;

namespace RealtimeChat.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;

        public ChatService(ApplicationDbContext context)
        {
            _context = context;
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

            invitation.Status = InvitationStatus.Declined;
            invitation.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<List<ChatRoom>> SearchRoomsAsync(string searchTerm, string? userId = null)
        {
            var query = _context.ChatRooms
                .Where(r => r.Name.Contains(searchTerm) ||
                           (r.Description != null && r.Description.Contains(searchTerm)));

            // If userId is provided, include user's membership info
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Include(r => r.Members.Where(m => m.UserId == userId));
            }

            return await query
                .Include(r => r.Members)
                .ThenInclude(m => m.User)
                .OrderBy(r => r.Name)
                .Take(20) // Limit results
                .ToListAsync();
        }

        public async Task<bool> LeaveRoomAsync(int roomId, string userId)
        {
            var membership = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(crm => crm.ChatRoomId == roomId && crm.UserId == userId);

            if (membership == null)
                return false;

            var room = await _context.ChatRooms
                .Include(r => r.Members)
                .FirstOrDefaultAsync(r => r.Id == roomId);

            if (room == null)
                return false;

            var adminCount = room.Members.Count(m => m.IsAdmin);
            var totalMembers = room.Members.Count;

            // If user is admin
            if (membership.IsAdmin)
            {
                // If user is the only admin and not the only member
                if (adminCount == 1 && totalMembers > 1)
                {
                    // Can't leave while being the only admin
                    return false;
                }

                // If user is the only member (and admin), delete the room
                if (totalMembers == 1)
                {
                    _context.ChatRooms.Remove(room);
                    await _context.SaveChangesAsync();
                    return true;
                }
            }

            // In all other cases, remove membership
            _context.ChatRoomMembers.Remove(membership);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ApplicationUser>> GetOnlineUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsOnline)
                .OrderBy(u => u.DisplayName ?? u.UserName)
                .ToListAsync();
        }
        public async Task<List<ApplicationUser>> GetRoomMembersAsync(int roomId)
        {
            return await _context.ChatRoomMembers
                .Where(crm => crm.ChatRoomId == roomId)
                .Include(crm => crm.User)
                .Select(crm => crm.User)
                .OrderBy(u => u.DisplayName ?? u.UserName)
                .ToListAsync();
        }


        public async Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm)
        {
            return await _context.Users
                .Where(u => (u.DisplayName != null && u.DisplayName.Contains(searchTerm)) ||
                           (u.UserName != null && u.UserName.Contains(searchTerm))||(u.Email!=null && u.Email.Contains(searchTerm)))
                .Take(10)
                .ToListAsync();
        }

        public async Task<bool> IsRoomAdminAsync(int roomId, string userId)
        {
            return await _context.ChatRoomMembers
                .AnyAsync(crm => crm.ChatRoomId == roomId &&
                                crm.UserId == userId &&
                                crm.IsAdmin);
        }

        public async Task<bool> IsRoomMemberAsync(int roomId, string userId)
        {
            return await _context.ChatRoomMembers
                .AnyAsync(crm => crm.ChatRoomId == roomId && crm.UserId == userId);
        }

        public async Task<RoomInvitation> GetInvitationDetailsAsync(int invitationId)
        {
            return await _context.RoomInvitations
                .Include(ri => ri.ChatRoom)
                .Include(ri => ri.Inviter)
                .FirstOrDefaultAsync(ri => ri.Id == invitationId);
        }
    }

}
