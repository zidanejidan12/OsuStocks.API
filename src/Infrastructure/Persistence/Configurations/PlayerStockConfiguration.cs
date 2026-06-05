using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class PlayerStockConfiguration : IEntityTypeConfiguration<PlayerStock>
{
    public void Configure(EntityTypeBuilder<PlayerStock> builder)
    {
        builder.ToTable("player_stocks");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TrackedPlayerId).HasColumnName("tracked_player_id").IsRequired();
        builder.Property(x => x.CurrentPrice).HasColumnName("current_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DemandScore).HasColumnName("demand_score").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.PerformanceScore).HasColumnName("performance_score").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

        builder.HasIndex(x => x.TrackedPlayerId).IsUnique().HasDatabaseName("uq_player_stock_player");
        builder.HasIndex(x => x.CurrentPrice).HasDatabaseName("ix_player_stock_price");

        builder.HasMany(x => x.PriceHistoryEntries)
            .WithOne(x => x.Stock)
            .HasForeignKey(x => x.StockId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Holdings)
            .WithOne(x => x.Stock)
            .HasForeignKey(x => x.StockId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Trades)
            .WithOne(x => x.Stock)
            .HasForeignKey(x => x.StockId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.MarketEvents)
            .WithOne(x => x.Stock)
            .HasForeignKey(x => x.StockId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
