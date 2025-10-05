using RealtimeChat.Data;
using RealtimeChat.Models;

namespace RealtimeChat.Services
{
    public interface IChatService
    {
        // core methods
        Task<List<ChatRoom>> GetPublicRoomsAsync();
        Task<List<ChatRoom>> GetUserRoomsAsync(string userId);
        Task<List<Message>> GetRoomMessagesAsync(int roomId, int page = 1, int pageSize = 50);
        Task<List<Message>> GetPrivateMessagesAsync(string userId1, string userId2, int page = 1, int pageSize = 50);
        Task<ChatRoom> CreateRoomAsync(string name, string? description, bool isPrivate, string createdById);
        Task<List<ApplicationUser>> GetOnlineUsersAsync();
        Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm);
        Task<ChatRoom> GetRoomAsync(int roomId);

        // Enhanced join room - only for public rooms
        Task<bool> JoinPublicRoomAsync(int roomId, string userId);

        // New invitation system for private rooms
        Task<bool> InviteToRoomAsync(int roomId, string inviterId, string inviteeId);
        Task<List<RoomInvitation>> GetPendingInvitationsAsync(string userId);
        Task<bool> AcceptInvitationAsync(int invitationId, string userId);
        Task<bool> DeclineInvitationAsync(int invitationId, string userId);

        // Room search functionality
        Task<List<ChatRoom>> SearchRoomsAsync(string searchTerm, string? userId = null);

        // Leave room functionality
        Task<bool> LeaveRoomAsync(int roomId, string userId);

        // Admin check
        Task<bool> IsRoomAdminAsync(int roomId, string userId);

        // Get room members
        Task<List<ApplicationUser>> GetRoomMembersAsync(int roomId);

        // Check if user is member of room
        Task<bool> IsRoomMemberAsync(int roomId, string userId);

        Task<RoomInvitation> GetInvitationDetailsAsync(int invitationId);


    }
}
