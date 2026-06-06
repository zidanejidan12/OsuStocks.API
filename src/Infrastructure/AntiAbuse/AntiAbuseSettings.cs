using Microsoft.Extensions.Options;
using OsuStocks.Application.Common.Interfaces;

namespace OsuStocks.Infrastructure.AntiAbuse;

internal sealed class AntiAbuseSettings(IOptions<AntiAbuseOptions> options) : IAntiAbuseSettings
{
    public decimal MaxOwnershipPercentage => options.Value.MaxOwnershipPercentage;
    public int TradeCooldownSeconds => options.Value.TradeCooldownSeconds;
    public int RapidTradeWindowSeconds => options.Value.RapidTradeWindowSeconds;
    public int RapidTradeThreshold => options.Value.RapidTradeThreshold;
}
