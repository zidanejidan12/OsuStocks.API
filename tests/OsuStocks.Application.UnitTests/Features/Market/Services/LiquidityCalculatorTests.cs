using OsuStocks.Domain.Market.Services;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Market.Services;

public sealed class LiquidityCalculatorTests
{
    [Fact]
    public void Liquidity_CombinesFloatAndWeightedVolume()
    {
        // 100 float + weight 2 * 50 volume = 200.
        Assert.Equal(200m, LiquidityCalculator.Liquidity(100m, 50m, 2m));
    }

    [Fact]
    public void Liquidity_ClampsNegativeInputsToZero()
    {
        Assert.Equal(0m, LiquidityCalculator.Liquidity(-5m, -10m, 1m));
    }

    [Theory]
    [InlineData(0, "Thin")]
    [InlineData(999, "Thin")]
    [InlineData(1000, "Moderate")]
    [InlineData(4999, "Moderate")]
    [InlineData(5000, "Deep")]
    public void Tier_BandsRelativeToReferenceDepth(double liquidity, string expected)
    {
        Assert.Equal(expected, LiquidityCalculator.Tier((decimal)liquidity, 1000m));
    }
}
