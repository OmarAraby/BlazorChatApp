using RealtimeChat.Data;
using RealtimeChat.Models;

namespace RealtimeChat.Services
{
    public interface IChatService
    {
        Task<List<ChatRoom>> GetPublicRoomsAsync();
        Task<List<ChatRoom>> GetUserRoomsAsync(string userId);
        Task<List<Message>> GetRoomMessagesAsync(int roomId, int page = 1, int pageSize = 50);
        Task<List<Message>> GetPrivateMessagesAsync(string userId1, string userId2, int page = 1, int pageSize = 50);
        Task<ChatRoom> CreateRoomAsync(string name, string? description, bool isPrivate, string createdById);
        Task<bool> JoinRoomAsync(int roomId, string userId);
        Task<List<ApplicationUser>> GetOnlineUsersAsync();
        Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm);
        Task<ChatRoom> GetRoomAsync(int roomId);
    }
}
