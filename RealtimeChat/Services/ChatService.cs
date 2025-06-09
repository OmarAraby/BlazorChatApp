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
            return await _context.ChatRoomMembers
                .Where(crm => crm.UserId == userId)
                .Include(crm => crm.ChatRoom)
                .ThenInclude(cr => cr.Members)
                .ThenInclude(m => m.User)
                .Select(crm => crm.ChatRoom)
                .Where(cr => !cr.IsPrivate || cr.CreatedById == userId || cr.Members.Any(m => m.UserId == userId))
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

        public async Task<bool> JoinRoomAsync(int roomId, string userId)
        {
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

        public async Task<List<ApplicationUser>> GetOnlineUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsOnline)
                .OrderBy(u => u.DisplayName ?? u.UserName)
                .ToListAsync();
        }

        public async Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm)
        {
            return await _context.Users
                .Where(u => (u.DisplayName != null && u.DisplayName.Contains(searchTerm)) ||
                           (u.UserName != null && u.UserName.Contains(searchTerm)))
                .Take(10)
                .ToListAsync();
        }
    }

}
