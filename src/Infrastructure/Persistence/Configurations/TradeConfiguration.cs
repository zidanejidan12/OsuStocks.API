using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("trades");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.StockId).HasColumnName("stock_id").IsRequired();
        builder.Property(x => x.TradeType).HasColumnName("trade_type").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ExecutedAt).HasColumnName("executed_at").IsRequired();

        builder.HasIndex(x => x.StockId).HasDatabaseName("ix_trade_stock");
        builder.HasIndex(x => new { x.UserId, x.ExecutedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_trade_user_executed_desc");
        builder.HasIndex(x => new { x.StockId, x.ExecutedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_trade_stock_executed");

        builder.HasOne(x => x.User)
            .WithMany(x => x.Trades)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
