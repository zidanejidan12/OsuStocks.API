using FluentValidation;

namespace OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;

public sealed class ListTrackedPlayersQueryValidator : AbstractValidator<ListTrackedPlayersQuery>
{
    public ListTrackedPlayersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
