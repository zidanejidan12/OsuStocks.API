using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class MarketSettingsConfiguration : IEntityTypeConfiguration<MarketSettings>
{
    public void Configure(EntityTypeBuilder<MarketSettings> builder)
    {
        builder.ToTable("market_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PpMultiplier).HasColumnName("pp_multiplier").HasColumnType("numeric(10,4)").IsRequired();
        builder.Property(x => x.TradeMultiplier).HasColumnName("trade_multiplier").HasColumnType("numeric(10,4)").IsRequired();
        builder.Property(x => x.DecayMultiplier).HasColumnName("decay_multiplier").HasColumnType("numeric(10,4)").IsRequired();
        builder.Property(x => x.TradeFeeMultiplier).HasColumnName("trade_fee_multiplier").HasColumnType("numeric(10,4)").HasDefaultValue(1m).IsRequired();
        builder.Property(x => x.IsMaintenanceMode).HasColumnName("is_maintenance_mode").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
    }
}
