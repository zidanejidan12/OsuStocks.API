using OsuStocks.Domain.Market.Services;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Market.Services;

public sealed class InitialStockPriceCalculatorTests
{
    [Fact]
    public void Compute_Rank1_IsAboutTopPrice()
    {
        var price = InitialStockPriceCalculator.Compute(1);

        Assert.Equal(1000m, price);
    }

    [Fact]
    public void Compute_Rank500_IsAboutBaseline()
    {
        var price = InitialStockPriceCalculator.Compute(500);

        // Curve is tuned so rank ~500 lands near the 100-credit neutral baseline.
        Assert.InRange(price, 90m, 110m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Compute_UnrankedOrInvalid_ReturnsNeutralBaseline(int? globalRank)
    {
        var price = InitialStockPriceCalculator.Compute(globalRank);

        Assert.Equal(100m, price);
    }

    [Fact]
    public void Compute_IsMonotonicallyDecreasingWithRank()
    {
        var betterRank = InitialStockPriceCalculator.Compute(10);
        var worseRank = InitialStockPriceCalculator.Compute(1000);

        Assert.True(betterRank > worseRank);
    }

    [Fact]
    public void Compute_VeryLargeRank_NeverGoesBelowFloor()
    {
        var price = InitialStockPriceCalculator.Compute(int.MaxValue);

        Assert.True(price >= 1m);
    }
}
