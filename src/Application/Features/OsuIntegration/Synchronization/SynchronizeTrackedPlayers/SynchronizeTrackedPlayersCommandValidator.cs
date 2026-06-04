using FluentValidation;

namespace OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;

public sealed class SynchronizeTrackedPlayersCommandValidator : AbstractValidator<SynchronizeTrackedPlayersCommand>
{
    public SynchronizeTrackedPlayersCommandValidator()
    {
    }
}
