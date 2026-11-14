using Microsoft.EntityFrameworkCore;
using TimeBlock.Core.Entities;

namespace TimeBlock.Core.Data;

public class TimeBlockDbContext(DbContextOptions<TimeBlockDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<BlockTemplate> Templates => Set<BlockTemplate>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasIndex(g => g.InviteCode).IsUnique();
            entity.Property(g => g.Name).HasMaxLength(128);
            entity.Property(g => g.InviteCode).HasMaxLength(16);
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(m => new { m.GroupId, m.UserId });
        });

        modelBuilder.Entity<BlockTemplate>(entity =>
        {
            entity.Property(t => t.Title).HasMaxLength(200);
            entity.Property(t => t.Category).HasMaxLength(64);
            entity.Property(t => t.Rrule).HasMaxLength(256);
        });

        modelBuilder.Entity<Block>(entity =>
        {
            entity.Property(b => b.Title).HasMaxLength(200);
            entity.Property(b => b.Category).HasMaxLength(64);
            entity.HasIndex(b => new { b.TemplateId, b.StartUtc });
            entity.HasIndex(b => new { b.OwnerId, b.StartUtc });
            entity.HasIndex(b => b.GroupId);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => new { n.UserId, n.IsRead });
            entity.Property(n => n.Type).HasMaxLength(32);
            entity.Property(n => n.Message).HasMaxLength(512);
        });
    }
}
