using RealtimeChat.Data;

namespace RealtimeChat.Models
{
    public class RoomInvitation
    {
        public int Id { get; set; }
        public int ChatRoomId { get; set; }
        public ChatRoom ChatRoom { get; set; } = null!;
        public string InviterId { get; set; } = string.Empty;
        public ApplicationUser Inviter { get; set; } = null!;
        public string InviteeId { get; set; } = string.Empty;
        public ApplicationUser Invitee { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
        public DateTime? RespondedAt { get; set; }
    }

    public enum InvitationStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired
    }
}
