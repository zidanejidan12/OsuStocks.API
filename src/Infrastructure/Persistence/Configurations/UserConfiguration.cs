using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OsuUserId).HasColumnName("osu_user_id").IsRequired();
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(512);
        builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

        builder.HasIndex(x => x.OsuUserId).IsUnique().HasDatabaseName("uq_users_osu_user_id");
        builder.HasIndex(x => x.Username).HasDatabaseName("ix_users_username");

        builder.HasOne(x => x.Wallet)
            .WithOne(x => x.User)
            .HasForeignKey<Wallet>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Portfolio)
            .WithOne(x => x.User)
            .HasForeignKey<Portfolio>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
