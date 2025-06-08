using RealtimeChat.Data;

namespace RealtimeChat.Models
{
    public class Message
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public string SenderId { get; set; } = string.Empty;
        public ApplicationUser Sender { get; set; } = null!;
        public string? RecipientId { get; set; }
        public ApplicationUser? Recipient { get; set; }
        public int? ChatRoomId { get; set; }
        public ChatRoom? ChatRoom { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsRead { get; set; }
    }
}
