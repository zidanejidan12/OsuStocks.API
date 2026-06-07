using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class WealthSnapshotConfiguration : IEntityTypeConfiguration<WealthSnapshot>
{
    public void Configure(EntityTypeBuilder<WealthSnapshot> builder)
    {
        builder.ToTable("user_wealth_snapshots");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.CapturedAt).HasColumnName("captured_at").IsRequired();
        builder.Property(x => x.Wealth).HasColumnName("wealth").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.NetDeposits).HasColumnName("net_deposits").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Profit).HasColumnName("profit").HasPrecision(18, 2).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.CapturedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_wealth_snapshot_user_captured_desc");
    }
}
