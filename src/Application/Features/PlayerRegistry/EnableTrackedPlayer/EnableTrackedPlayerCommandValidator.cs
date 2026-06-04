using FluentValidation;

namespace OsuStocks.Application.Features.PlayerRegistry.EnableTrackedPlayer;

public sealed class EnableTrackedPlayerCommandValidator : AbstractValidator<EnableTrackedPlayerCommand>
{
    public EnableTrackedPlayerCommandValidator()
    {
        RuleFor(x => x.TrackedPlayerId)
            .NotEmpty();

        RuleFor(x => x.Actor)
            .MaximumLength(100);
    }
}
