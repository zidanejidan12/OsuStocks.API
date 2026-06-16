using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("holdings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PortfolioId).HasColumnName("portfolio_id").IsRequired();
        builder.Property(x => x.StockId).HasColumnName("stock_id").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.AveragePrice).HasColumnName("average_price").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

        builder.HasIndex(x => new { x.PortfolioId, x.StockId }).IsUnique().HasDatabaseName("uq_holding_portfolio_stock");
        builder.HasIndex(x => x.StockId).HasDatabaseName("ix_holding_stock");
    }
}
