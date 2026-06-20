using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Admin.MarketSettings.UpdateMarketSettings;

public sealed class UpdateMarketSettingsCommandHandler(
    IMarketSettingsRepository marketSettingsRepository,
    IApplicationDbContext dbContext)
    : IRequestHandler<UpdateMarketSettingsCommand, Result<UpdateMarketSettingsResponse>>
{
    public async Task<Result<UpdateMarketSettingsResponse>> Handle(
        UpdateMarketSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var actor = string.IsNullOrWhiteSpace(request.Actor) ? "admin" : request.Actor;

        var settings = await marketSettingsRepository.GetCurrentForUpdateAsync(cancellationToken);
        if (settings is null)
        {
            settings = new OsuStocks.Domain.Entities.MarketSettings
            {
                Id = Guid.NewGuid(),
                PpMultiplier = request.PpMultiplier,
                TradeMultiplier = request.TradeMultiplier,
                DecayMultiplier = request.DecayMultiplier,
                TradeFeeMultiplier = request.TradeFeeMultiplier,
                IsMaintenanceMode = request.IsMaintenanceMode,
                CreatedAt = now,
                CreatedBy = actor
            };

            await marketSettingsRepository.AddAsync(settings, cancellationToken);
        }
        else
        {
            settings.PpMultiplier = request.PpMultiplier;
            settings.TradeMultiplier = request.TradeMultiplier;
            settings.DecayMultiplier = request.DecayMultiplier;
            settings.TradeFeeMultiplier = request.TradeFeeMultiplier;
            settings.IsMaintenanceMode = request.IsMaintenanceMode;
            settings.UpdatedAt = now;
            settings.UpdatedBy = actor;

            marketSettingsRepository.Update(settings);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new UpdateMarketSettingsResponse(
            settings.PpMultiplier,
            settings.TradeMultiplier,
            settings.DecayMultiplier,
            settings.TradeFeeMultiplier,
            settings.IsMaintenanceMode));
    }
}
