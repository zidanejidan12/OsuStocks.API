using Microsoft.Extensions.Options;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Infrastructure.Market.Options;

namespace OsuStocks.Infrastructure.Market;

internal sealed class InactivityDecaySettings(IOptions<MarketEngineOptions> options) : IInactivityDecaySettings
{
    public int InactivityThresholdDays => options.Value.InactivityThresholdDays;
}
