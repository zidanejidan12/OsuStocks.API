using Microsoft.EntityFrameworkCore;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.Common.Interfaces;
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
    public DbSet<MarketSettings> MarketSettings => Set<MarketSettings>();
    public DbSet<WealthSnapshot> WealthSnapshots => Set<WealthSnapshot>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<InvestorProfile> InvestorProfiles => Set<InvestorProfile>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<UserMissionCompletion> UserMissionCompletions => Set<UserMissionCompletion>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyRowVersioning();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private void ApplyRowVersioning()
    {
        foreach (var entry in ChangeTracker.Entries<IHasRowVersion>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.RowVersion <= 0)
                {
                    entry.Entity.RowVersion = 1;
                }

                continue;
            }

            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var rowVersionProperty = entry.Property(x => x.RowVersion);
            if (rowVersionProperty.OriginalValue <= 0)
            {
                rowVersionProperty.OriginalValue = entry.Entity.RowVersion;
            }

            var currentVersion = Math.Max(rowVersionProperty.OriginalValue, entry.Entity.RowVersion);
            entry.Entity.RowVersion = checked(currentVersion + 1);
            rowVersionProperty.IsModified = true;
        }
    }
}
