using FluentValidation;

namespace OsuStocks.Application.Features.PlayerRegistry.DisableTrackedPlayer;

public sealed class DisableTrackedPlayerCommandValidator : AbstractValidator<DisableTrackedPlayerCommand>
{
    public DisableTrackedPlayerCommandValidator()
    {
        RuleFor(x => x.TrackedPlayerId)
            .NotEmpty();

        RuleFor(x => x.Actor)
            .MaximumLength(100);
    }
}
