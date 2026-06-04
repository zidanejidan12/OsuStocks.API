using FluentValidation;

namespace OsuStocks.Application.Features.PlayerRegistry.AddTrackedPlayer;

public sealed class AddTrackedPlayerCommandValidator : AbstractValidator<AddTrackedPlayerCommand>
{
    public AddTrackedPlayerCommandValidator()
    {
        RuleFor(x => x.OsuUserId)
            .GreaterThan(0)
            .WithMessage("osu user id must be greater than 0.");

        RuleFor(x => x.TrackingTier)
            .IsInEnum();

        RuleFor(x => x.Actor)
            .MaximumLength(100);
    }
}
