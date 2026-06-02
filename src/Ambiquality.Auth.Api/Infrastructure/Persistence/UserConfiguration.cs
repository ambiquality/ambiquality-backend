using Ambiquality.Auth.Api.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambiquality.Auth.Api.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for the <see cref="User"/> aggregate. Maps the
/// <see cref="Email"/> value object via string conversion and the refresh /
/// verification tokens as owned collections backed by private fields.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.Create(value))
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PendingEmail)
            .HasConversion(
                email => email == null ? null : email.Value,
                value => value == null ? null : Email.Create(value))
            .HasColumnName("pending_email")
            .HasMaxLength(320);

        builder.Property(u => u.EmailConfirmed)
            .HasColumnName("email_confirmed")
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(u => u.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(u => u.LastFailedLoginAt)
            .HasColumnName("last_failed_login_at");

        // Owned refresh tokens, backed by the private _refreshTokens field.
        builder.OwnsMany<RefreshToken>("RefreshTokens", rt =>
        {
            rt.ToTable("refresh_tokens");
            rt.WithOwner().HasForeignKey("user_id");
            rt.HasKey(t => t.Id);
            rt.Property(t => t.Id).ValueGeneratedNever();
            rt.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
            rt.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            rt.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
            rt.Property(t => t.RevokedAt).HasColumnName("revoked_at");
            rt.HasIndex(t => t.TokenHash);
        });
        builder.Navigation("RefreshTokens")
            .HasField("_refreshTokens")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Owned verification tokens, backed by the private _verificationTokens field.
        builder.OwnsMany<VerificationToken>("VerificationTokens", vt =>
        {
            vt.ToTable("verification_tokens");
            vt.WithOwner().HasForeignKey("user_id");
            vt.HasKey(t => t.Id);
            vt.Property(t => t.Id).ValueGeneratedNever();
            vt.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
            vt.Property(t => t.Purpose).HasColumnName("purpose").HasConversion<int>().IsRequired();
            vt.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            vt.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
            vt.Property(t => t.ConsumedAt).HasColumnName("consumed_at");
            vt.HasIndex(t => t.TokenHash);
        });
        builder.Navigation("VerificationTokens")
            .HasField("_verificationTokens")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
