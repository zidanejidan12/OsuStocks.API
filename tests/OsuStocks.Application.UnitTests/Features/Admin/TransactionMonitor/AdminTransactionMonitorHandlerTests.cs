using OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminTrades;
using OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminWalletTransactions;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Admin.TransactionMonitor;

public sealed class AdminTransactionMonitorHandlerTests
{
    [Fact]
    public async Task GetAdminTrades_ComputesSkipFromPage_AndMapsFields()
    {
        var repo = new FakeAdminTransactionReadRepository
        {
            Trades =
            [
                new AdminTradeReadModel(
                    Guid.NewGuid(), Guid.NewGuid(), "alice", "avatar.png",
                    Guid.NewGuid(), "mrekk", TradeType.Buy, 2.5m, 100m, 250m, DateTimeOffset.UtcNow)
            ],
            TradesTotalCount = 57
        };
        var handler = new GetAdminTradesQueryHandler(repo);

        var result = await handler.Handle(
            new GetAdminTradesQuery(Page: 3, PageSize: 25, TradeType: TradeType.Buy), default);

        Assert.True(result.IsSuccess);
        // page 3, size 25 → skip 50
        Assert.Equal(50, repo.LastTradesSkip);
        Assert.Equal(25, repo.LastTradesTake);
        Assert.Equal(TradeType.Buy, repo.LastTradesType);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("alice", item.Username);
        Assert.Equal("mrekk", item.PlayerName);
        Assert.Equal("Buy", item.TradeType); // enum projected to string
        Assert.Equal(57, result.Value.TotalCount);
        Assert.Equal(3, result.Value.Page);
    }

    [Fact]
    public async Task GetAdminWalletTransactions_ComputesSkip_AndMapsEnumToString()
    {
        var repo = new FakeAdminTransactionReadRepository
        {
            WalletTransactions =
            [
                new AdminWalletTransactionReadModel(
                    Guid.NewGuid(), Guid.NewGuid(), "bob",
                    WalletTransactionType.TradeFee, -42m, Guid.NewGuid(), DateTimeOffset.UtcNow)
            ],
            WalletTotalCount = 9
        };
        var handler = new GetAdminWalletTransactionsQueryHandler(repo);

        var result = await handler.Handle(
            new GetAdminWalletTransactionsQuery(Page: 1, PageSize: 50, TransactionType: WalletTransactionType.TradeFee), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repo.LastWalletSkip);
        Assert.Equal(50, repo.LastWalletTake);
        Assert.Equal(WalletTransactionType.TradeFee, repo.LastWalletType);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("bob", item.Username);
        Assert.Equal("TradeFee", item.TransactionType);
        Assert.Equal(-42m, item.Amount);
        Assert.Equal(9, result.Value.TotalCount);
    }

    private sealed class FakeAdminTransactionReadRepository : IAdminTransactionReadRepository
    {
        public IReadOnlyList<AdminTradeReadModel> Trades { get; set; } = [];
        public int TradesTotalCount { get; set; }
        public int LastTradesSkip { get; private set; }
        public int LastTradesTake { get; private set; }
        public TradeType? LastTradesType { get; private set; }

        public IReadOnlyList<AdminWalletTransactionReadModel> WalletTransactions { get; set; } = [];
        public int WalletTotalCount { get; set; }
        public int LastWalletSkip { get; private set; }
        public int LastWalletTake { get; private set; }
        public WalletTransactionType? LastWalletType { get; private set; }

        public Task<(IReadOnlyList<AdminTradeReadModel> Items, int TotalCount)> GetTradesAsync(
            Guid? userId, Guid? stockId, TradeType? tradeType, DateTimeOffset? from, DateTimeOffset? to,
            int skip, int take, CancellationToken cancellationToken = default)
        {
            LastTradesSkip = skip;
            LastTradesTake = take;
            LastTradesType = tradeType;
            return Task.FromResult((Trades, TradesTotalCount));
        }

        public Task<(IReadOnlyList<AdminWalletTransactionReadModel> Items, int TotalCount)> GetWalletTransactionsAsync(
            Guid? userId, WalletTransactionType? transactionType, DateTimeOffset? from, DateTimeOffset? to,
            int skip, int take, CancellationToken cancellationToken = default)
        {
            LastWalletSkip = skip;
            LastWalletTake = take;
            LastWalletType = transactionType;
            return Task.FromResult((WalletTransactions, WalletTotalCount));
        }
    }
}
