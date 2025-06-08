using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Models;

namespace RealtimeChat.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ChatRoom> ChatRooms { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ChatRoomMember> ChatRoomMembers { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Message relationships
        builder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasOne(m => m.Recipient)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(m => m.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasOne(m => m.ChatRoom)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ChatRoom relationships
        builder.Entity<ChatRoom>()
            .HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure ChatRoomMember relationships
        builder.Entity<ChatRoomMember>()
            .HasOne(crm => crm.User)
            .WithMany(u => u.ChatRoomMemberships)
            .HasForeignKey(crm => crm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChatRoomMember>()
            .HasOne(crm => crm.ChatRoom)
            .WithMany(c => c.Members)
            .HasForeignKey(crm => crm.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint for ChatRoomMember
        builder.Entity<ChatRoomMember>()
            .HasIndex(crm => new { crm.UserId, crm.ChatRoomId })
            .IsUnique();
    }
}
