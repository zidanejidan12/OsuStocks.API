using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence.Configurations;

internal sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WalletId).HasColumnName("wallet_id").IsRequired();
        builder.Property(x => x.TransactionType).HasColumnName("transaction_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.WalletId).HasDatabaseName("ix_wallet_transactions_wallet_id");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_wallet_transactions_created_at");
    }
}
