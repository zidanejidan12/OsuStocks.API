using FluentValidation;

namespace OsuStocks.Application.Features.PlayerRegistry.SearchOsuPlayers;

public sealed class SearchOsuPlayersQueryValidator : AbstractValidator<SearchOsuPlayersQuery>
{
    public SearchOsuPlayersQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50);
    }
}
