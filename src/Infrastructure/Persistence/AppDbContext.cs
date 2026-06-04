using Microsoft.EntityFrameworkCore;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.Entities;

namespace OsuStocks.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<TrackedPlayer> TrackedPlayers => Set<TrackedPlayer>();
    public DbSet<PlayerStock> PlayerStocks => Set<PlayerStock>();
    public DbSet<StockPriceHistory> StockPriceHistory => Set<StockPriceHistory>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<PlayerSnapshot> PlayerSnapshots => Set<PlayerSnapshot>();
    public DbSet<MarketEvent> MarketEvents => Set<MarketEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
