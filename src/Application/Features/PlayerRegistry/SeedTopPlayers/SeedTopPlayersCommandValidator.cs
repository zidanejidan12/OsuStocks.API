using FluentValidation;

namespace OsuStocks.Application.Features.PlayerRegistry.SeedTopPlayers;

public sealed class SeedTopPlayersCommandValidator : AbstractValidator<SeedTopPlayersCommand>
{
    public SeedTopPlayersCommandValidator()
    {
        // osu! performance rankings cap at the top 10,000 (page 200 × 50).
        RuleFor(x => x.Count)
            .InclusiveBetween(1, 10_000)
            .WithMessage("Count must be between 1 and 10000.");

        RuleFor(x => x.Actor)
            .MaximumLength(100);
    }
}
