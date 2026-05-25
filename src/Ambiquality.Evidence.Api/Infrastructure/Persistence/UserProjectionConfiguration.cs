using Ambiquality.Evidence.Api.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class UserProjectionConfiguration : IEntityTypeConfiguration<UserProjection>
{
    public void Configure(EntityTypeBuilder<UserProjection> builder)
    {
        builder.ToTable("user_projections");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.AuthUserId)
            .HasColumnName("auth_user_id")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(u => u.AuthUserId)
            .IsUnique()
            .HasDatabaseName("IX_user_projections_auth_user_id_unique");
    }
}
