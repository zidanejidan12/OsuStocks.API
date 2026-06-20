using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Admin.MarketSettings.GetMarketSettings;

public sealed class GetMarketSettingsQueryHandler(IMarketSettingsRepository marketSettingsRepository)
    : IRequestHandler<GetMarketSettingsQuery, Result<GetMarketSettingsResponse>>
{
    public async Task<Result<GetMarketSettingsResponse>> Handle(
        GetMarketSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await marketSettingsRepository.GetCurrentAsync(cancellationToken);

        return Result.Success(new GetMarketSettingsResponse(
            settings?.PpMultiplier ?? 1m,
            settings?.TradeMultiplier ?? 1m,
            settings?.DecayMultiplier ?? 1m,
            settings?.TradeFeeMultiplier ?? 1m,
            settings?.IsMaintenanceMode ?? false));
    }
}
