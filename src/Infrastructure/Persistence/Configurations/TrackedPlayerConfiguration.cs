using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class TrackedPlayerConfiguration : IEntityTypeConfiguration<TrackedPlayer>
{
    public void Configure(EntityTypeBuilder<TrackedPlayer> builder)
    {
        builder.ToTable("tracked_players");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OsuUserId).HasColumnName("osu_user_id").IsRequired();
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(512);
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(x => x.TrackingTier).HasColumnName("tracking_tier").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(x => x.LastInactivityDecayAt).HasColumnName("last_inactivity_decay_at");

        builder.HasIndex(x => x.OsuUserId).IsUnique().HasDatabaseName("uq_tracked_players_osu_user_id");
        builder.HasIndex(x => new { x.IsActive, x.TrackingTier, x.Username })
            .HasDatabaseName("ix_tracked_players_active_tier_username");

        builder.HasOne(x => x.Stock)
            .WithOne(x => x.TrackedPlayer)
            .HasForeignKey<PlayerStock>(x => x.TrackedPlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Snapshots)
            .WithOne(x => x.TrackedPlayer)
            .HasForeignKey(x => x.TrackedPlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
