namespace OsuStocks.Application.Common.Interfaces;

public interface IInactivityDecaySettings
{
    int InactivityThresholdDays { get; }
}
