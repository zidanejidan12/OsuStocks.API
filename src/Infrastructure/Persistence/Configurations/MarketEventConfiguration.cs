using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class MarketEventConfiguration : IEntityTypeConfiguration<MarketEvent>
{
    public void Configure(EntityTypeBuilder<MarketEvent> builder)
    {
        builder.ToTable("market_events");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.StockId).HasColumnName("stock_id").IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.StockId).HasDatabaseName("ix_market_events_stock");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_market_events_created");
    }
}
