namespace RealtimeChat.Dtos
{
    public class MessageDto
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public string SenderId { get; set; }
        public string RecipientId { get; set; }

        public string SenderName { get; set; }
        public DateTime SentAt { get; set; }
        public int RoomId { get; set; }

    }
}
