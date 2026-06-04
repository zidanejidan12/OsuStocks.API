using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.HandleOsuCallback;

public sealed record HandleOsuCallbackCommand(string Code, string State)
    : IRequest<Result<HandleOsuCallbackResponse>>;
