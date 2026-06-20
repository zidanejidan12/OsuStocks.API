using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Market.Services;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Market.Services;

public sealed class TradeFeeCalculatorTests
{
    // Simple progressive schedule: 10% up to 100, 20% up to 1000, 30% above (top bracket unbounded).
    private static readonly IReadOnlyList<TradeFeeBracket> Brackets =
    [
        new TradeFeeBracket(100m, 0.10m),
        new TradeFeeBracket(1000m, 0.20m),
        new TradeFeeBracket(1_000_000m, 0.30m)
    ];

    [Fact]
    public void Compute_WithinFirstBracket_ChargesFirstRate()
    {
        // 50 * 10% = 5.
        Assert.Equal(5m, TradeFeeCalculator.Compute(50m, Brackets, 1m));
    }

    [Fact]
    public void Compute_SpansBrackets_IsMarginal()
    {
        // 500 -> 100*10% + 400*20% = 10 + 80 = 90.
        Assert.Equal(90m, TradeFeeCalculator.Compute(500m, Brackets, 1m));
    }

    [Fact]
    public void Compute_AboveTopThreshold_TopBracketIsUnbounded()
    {
        // 2000 -> 100*10% + 900*20% + 1000*30% = 10 + 180 + 300 = 490.
        Assert.Equal(490m, TradeFeeCalculator.Compute(2000m, Brackets, 1m));
    }

    [Fact]
    public void Compute_LargeTradePaysHigherEffectiveRateThanSmall()
    {
        var small = TradeFeeCalculator.Compute(50m, Brackets, 1m) / 50m;       // 10%
        var large = TradeFeeCalculator.Compute(2000m, Brackets, 1m) / 2000m;   // ~24.5%

        Assert.True(large > small);
    }

    [Fact]
    public void Compute_AppliesMultiplier()
    {
        // 500 -> 90 base; x2 = 180; x0.5 = 45.
        Assert.Equal(180m, TradeFeeCalculator.Compute(500m, Brackets, 2m));
        Assert.Equal(45m, TradeFeeCalculator.Compute(500m, Brackets, 0.5m));
    }

    [Theory]
    [InlineData(0)]    // disabled
    [InlineData(-1)]
    public void Compute_NonPositiveMultiplier_IsZero(decimal multiplier)
    {
        Assert.Equal(0m, TradeFeeCalculator.Compute(500m, Brackets, multiplier));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Compute_NonPositiveValue_IsZero(decimal tradeValue)
    {
        Assert.Equal(0m, TradeFeeCalculator.Compute(tradeValue, Brackets, 1m));
    }

    [Fact]
    public void Compute_NoBrackets_IsZero()
    {
        Assert.Equal(0m, TradeFeeCalculator.Compute(500m, [], 1m));
    }
}
