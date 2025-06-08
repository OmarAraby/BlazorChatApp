using RealtimeChat.Data;

namespace RealtimeChat.Models
{
    public class ChatRoomMember
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
        public int ChatRoomId { get; set; }
        public ChatRoom ChatRoom { get; set; } = null!;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsAdmin { get; set; }

    }
}
