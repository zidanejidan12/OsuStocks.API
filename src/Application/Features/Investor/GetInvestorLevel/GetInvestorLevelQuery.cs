using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Investor.GetInvestorLevel;

public sealed record GetInvestorLevelQuery(Guid UserId)
    : IRequest<Result<GetInvestorLevelResponse>>;
