using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthAuditLog> AuthAuditLogs => Set<AuthAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            entity.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(u => u.Role)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("user");

            entity.Property(u => u.IsActive)
                .HasDefaultValue(true);

            entity.Property(u => u.FailedLoginAttempts)
                .HasDefaultValue(0);

            entity.Property(u => u.LockoutEndAt);

            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasMany(u => u.RefreshTokens)
                .WithOne(rt => rt.User)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);

            entity.Property(rt => rt.TokenHash)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(rt => rt.TokenHash)
                .IsUnique();

            entity.HasIndex(rt => rt.UserId);

            entity.Property(rt => rt.ExpiresAt)
                .IsRequired();

            entity.Property(rt => rt.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(rt => rt.ReplacedByTokenHash)
                .HasMaxLength(200);
        });

        modelBuilder.Entity<AuthAuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.EventType)
                .IsRequired()
                .HasMaxLength(40);

            entity.Property(a => a.Email)
                .HasMaxLength(256);

            entity.Property(a => a.FailureReason)
                .HasMaxLength(200);

            entity.Property(a => a.IpAddress)
                .HasMaxLength(45);

            entity.Property(a => a.UserAgent)
                .HasMaxLength(512);

            entity.Property(a => a.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(a => a.CreatedAt);

            entity.HasIndex(a => new { a.EventType, a.CreatedAt });

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
