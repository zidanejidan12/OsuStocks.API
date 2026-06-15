using FluentValidation;

namespace OsuStocks.Application.Features.PlayerRegistry.DeleteTrackedPlayer;

public sealed class DeleteTrackedPlayerCommandValidator : AbstractValidator<DeleteTrackedPlayerCommand>
{
    public DeleteTrackedPlayerCommandValidator()
    {
        RuleFor(x => x.TrackedPlayerId)
            .NotEmpty();
    }
}
