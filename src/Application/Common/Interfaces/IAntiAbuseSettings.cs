namespace OsuStocks.Application.Common.Interfaces;

public interface IAntiAbuseSettings
{
    decimal MaxOwnershipPercentage { get; }
    int TradeCooldownSeconds { get; }
    int RapidTradeWindowSeconds { get; }
    int RapidTradeThreshold { get; }
}
