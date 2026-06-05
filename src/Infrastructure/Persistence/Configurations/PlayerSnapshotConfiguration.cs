using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class PlayerSnapshotConfiguration : IEntityTypeConfiguration<PlayerSnapshot>
{
    public void Configure(EntityTypeBuilder<PlayerSnapshot> builder)
    {
        builder.ToTable("player_snapshots");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TrackedPlayerId).HasColumnName("tracked_player_id").IsRequired();
        builder.Property(x => x.CurrentPp).HasColumnName("current_pp").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.GlobalRank).HasColumnName("global_rank");
        builder.Property(x => x.TopScoreId).HasColumnName("top_score_id");
        builder.Property(x => x.TopScorePp).HasColumnName("top_score_pp").HasPrecision(18, 2);
        builder.Property(x => x.CapturedAt).HasColumnName("captured_at").IsRequired();

        builder.HasIndex(x => new { x.TrackedPlayerId, x.CapturedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_snapshot_player_captured_desc");
    }
}
