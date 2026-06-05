using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class WalletEndpointsTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetWallet_ReturnsCurrentBalance()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var walletRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();

        await walletRepository.AddAsync(new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Balance = 12345.67m,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        var response = await client.GetAsync("/api/v1/wallet");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WalletSummaryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(12345.67m, payload.Balance);
    }

    [Fact]
    public async Task GetWalletTransactions_ReturnsPagedLedgerItems()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var walletRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();
        var walletTransactionRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletTransactionRepository>();

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Balance = 1000m,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };

        await walletRepository.AddAsync(wallet);

        await walletTransactionRepository.AddAsync(new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionType = WalletTransactionType.BuyStock,
            Amount = 100m,
            ReferenceId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
        });

        await walletTransactionRepository.AddAsync(new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionType = WalletTransactionType.SellStock,
            Amount = 150m,
            ReferenceId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        var response = await client.GetAsync("/api/v1/wallet/transactions?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WalletTransactionsEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal("SellStock", payload.Items[0].TransactionType);
        Assert.Equal("BuyStock", payload.Items[1].TransactionType);
    }

    private sealed record WalletSummaryResponse([property: JsonPropertyName("balance")] decimal Balance);

    private sealed record WalletTransactionsEnvelope(
        [property: JsonPropertyName("items")] List<WalletTransactionItem> Items);

    private sealed record WalletTransactionItem(
        [property: JsonPropertyName("transactionId")] Guid TransactionId,
        [property: JsonPropertyName("transactionType")] string TransactionType,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("referenceId")] Guid? ReferenceId,
        [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);
}
