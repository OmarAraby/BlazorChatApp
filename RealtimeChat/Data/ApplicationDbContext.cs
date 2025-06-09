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
    public DbSet<RoomInvitation> RoomInvitations { get; set; }


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

        // Configure RoomInvitation relationships
        builder.Entity<RoomInvitation>()
            .HasOne(ri => ri.ChatRoom)
            .WithMany()
            .HasForeignKey(ri => ri.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RoomInvitation>()
            .HasOne(ri => ri.Inviter)
            .WithMany()
            .HasForeignKey(ri => ri.InviterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RoomInvitation>()
            .HasOne(ri => ri.Invitee)
            .WithMany()
            .HasForeignKey(ri => ri.InviteeId)
            .OnDelete(DeleteBehavior.Restrict);
        // Index for faster invitation queries
        builder.Entity<RoomInvitation>()
            .HasIndex(ri => new { ri.InviteeId, ri.Status });

        // Unique constraint to prevent duplicate pending invitations
        builder.Entity<RoomInvitation>()
            .HasIndex(ri => new { ri.ChatRoomId, ri.InviteeId, ri.Status })
            .IsUnique()
            .HasFilter("[Status] = 0"); // Only for pending invitations
    }
}
