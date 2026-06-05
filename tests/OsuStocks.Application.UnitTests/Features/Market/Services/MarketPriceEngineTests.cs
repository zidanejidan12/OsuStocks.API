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
        TopPlayImpact: 0.05m,
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
    public void Calculate_Inactivity_EnforcesPriceFloor()
    {
        var result = _engine.Calculate(1.20m, MarketPriceInput.Inactivity(), Coefficients);

        Assert.Equal(1m, result.NewPrice);
    }
}
