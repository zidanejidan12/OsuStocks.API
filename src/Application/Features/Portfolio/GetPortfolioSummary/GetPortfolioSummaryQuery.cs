using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Portfolio.GetPortfolioSummary;

public sealed record GetPortfolioSummaryQuery(Guid UserId)
    : IRequest<Result<GetPortfolioSummaryResponse>>;
