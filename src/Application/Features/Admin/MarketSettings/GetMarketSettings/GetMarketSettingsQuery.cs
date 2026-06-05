using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Admin.MarketSettings.GetMarketSettings;

public sealed record GetMarketSettingsQuery : IRequest<Result<GetMarketSettingsResponse>>;
