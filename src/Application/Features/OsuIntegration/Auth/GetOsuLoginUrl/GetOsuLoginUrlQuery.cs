using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetOsuLoginUrl;

public sealed record GetOsuLoginUrlQuery(string? ReturnUrl) : IRequest<Result<GetOsuLoginUrlResponse>>;
