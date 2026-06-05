using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class StockPriceHistoryConfiguration : IEntityTypeConfiguration<StockPriceHistory>
{
    public void Configure(EntityTypeBuilder<StockPriceHistory> builder)
    {
        builder.ToTable("stock_price_history");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.StockId).HasColumnName("stock_id").IsRequired();
        builder.Property(x => x.PreviousPrice).HasColumnName("previous_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.NewPrice).HasColumnName("new_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.StockId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_stock_history_stock_created_desc");
    }
}
