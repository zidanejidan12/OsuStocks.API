using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.OsuIntegration.InactivityDecay;

public sealed record EvaluateInactivityDecayCommand
    : IRequest<Result<EvaluateInactivityDecayResponse>>;

public sealed record EvaluateInactivityDecayResponse(
    int PlayersEvaluated,
    int DecayEventsPublished);
