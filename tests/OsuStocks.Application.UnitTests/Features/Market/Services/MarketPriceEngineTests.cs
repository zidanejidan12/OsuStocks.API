using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Market.Services;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Market.Services;

public sealed class MarketPriceEngineTests
{
    private readonly MarketPriceEngine _engine = new();

    private static readonly MarketPricingCoefficients Coefficients = new(
        TradeBuyImpactPerShare: 0.01m,
        TradeSellImpactPerShare: 0.02m,
        TopPlayImpactScale: 0.6m,
        MaxTopPlayImpact: 0.10m,
        MinTopPlayImpact: 0.005m,
        PpImpactPerPoint: 0.001m,
        MaxPpImpact: 0.10m,
        InactivityDecayImpact: 0.50m,
        PriceFloor: 1m);

    [Fact]
    public void Calculate_BuyOrder_IncreasesPriceUsingConfiguredCoefficient()
    {
        var result = _engine.Calculate(100m, MarketPriceInput.Buy(2), Coefficients);

        Assert.Equal(100m, result.PreviousPrice);
        Assert.Equal(102m, result.NewPrice);
        Assert.Equal(0.02m, result.PercentageChange);
    }

    [Fact]
    public void Calculate_PpIncrease_RespectsMaxImpactCap()
    {
        var result = _engine.Calculate(100m, MarketPriceInput.PpIncrease(1000m, 1200m), Coefficients);

        Assert.Equal(110m, result.NewPrice);
        Assert.Equal(0.10m, result.PercentageChange);
    }

    [Fact]
    public void Calculate_TopPlay_ScalesWithPlayPpRelativeToPlayerPp()
    {
        // ratio = 1000/10000 = 0.1; impact = 0.6 * 0.1 = 0.06 (within [min, max]).
        var result = _engine.Calculate(100m, MarketPriceInput.TopPlay(playPp: 1000m, playerPp: 10000m), Coefficients);

        Assert.Equal(0.06m, result.PercentageChange);
        Assert.Equal(106m, result.NewPrice);
    }

    [Fact]
    public void Calculate_TopPlay_BreakoutPlayIsCappedAtMax()
    {
        // ratio = 700/5000 = 0.14; impact = 0.6 * 0.14 = 0.084 -> capped at MaxTopPlayImpact 0.10? No: 0.084 < 0.10.
        // Use a larger fraction to exceed the cap: 800/3000 = 0.266 -> 0.6*0.266 = 0.16 -> clamped to 0.10.
        var result = _engine.Calculate(100m, MarketPriceInput.TopPlay(playPp: 800m, playerPp: 3000m), Coefficients);

        Assert.Equal(0.10m, result.PercentageChange);
    }

    [Fact]
    public void Calculate_TopPlay_FallsBackToFloorWhenPpUnknown()
    {
        var result = _engine.Calculate(100m, MarketPriceInput.TopPlay(playPp: 0m, playerPp: 0m), Coefficients);

        Assert.Equal(0.005m, result.PercentageChange);
    }

    [Fact]
    public void Calculate_Inactivity_EnforcesPriceFloor()
    {
        var result = _engine.Calculate(1.20m, MarketPriceInput.Inactivity(), Coefficients);

        Assert.Equal(1m, result.NewPrice);
    }
}
