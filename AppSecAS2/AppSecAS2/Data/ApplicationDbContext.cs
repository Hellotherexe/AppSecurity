using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BookwormsOnline.Models;

namespace BookwormsOnline.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Member> Members { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<PasswordHistory> PasswordHistories { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Member>(entity =>
        {
            // Configure unique constraint on Email
            entity.HasIndex(e => e.Email)
                  .IsUnique()
                  .HasDatabaseName("IX_Member_Email_Unique");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            // Configure index on MemberId for faster queries
            entity.HasIndex(e => e.MemberId)
                  .HasDatabaseName("IX_AuditLog_MemberId");

            // Configure index on TimestampUtc for date range queries
            entity.HasIndex(e => e.TimestampUtc)
                  .HasDatabaseName("IX_AuditLog_TimestampUtc");

            // Configure relationship
            entity.HasOne(a => a.Member)
                  .WithMany(m => m.AuditLogs)
                  .HasForeignKey(a => a.MemberId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PasswordHistory>(entity =>
        {
            // Configure index on MemberId for faster queries
            entity.HasIndex(e => e.MemberId)
                  .HasDatabaseName("IX_PasswordHistory_MemberId");

            // Configure index on ChangedAtUtc for date queries
            entity.HasIndex(e => e.ChangedAtUtc)
                  .HasDatabaseName("IX_PasswordHistory_ChangedAtUtc");

            // Configure relationship
            entity.HasOne(p => p.Member)
                  .WithMany(m => m.PasswordHistories)
                  .HasForeignKey(p => p.MemberId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            // Configure unique index on Token
            entity.HasIndex(e => e.Token)
                  .IsUnique()
                  .HasDatabaseName("IX_PasswordResetToken_Token_Unique");

            // Configure index on MemberId
            entity.HasIndex(e => e.MemberId)
                  .HasDatabaseName("IX_PasswordResetToken_MemberId");

            // Configure index on ExpiresAtUtc for cleanup queries
            entity.HasIndex(e => e.ExpiresAtUtc)
                  .HasDatabaseName("IX_PasswordResetToken_ExpiresAtUtc");

            // Configure relationship
            entity.HasOne(t => t.Member)
                  .WithMany(m => m.PasswordResetTokens)
                  .HasForeignKey(t => t.MemberId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
