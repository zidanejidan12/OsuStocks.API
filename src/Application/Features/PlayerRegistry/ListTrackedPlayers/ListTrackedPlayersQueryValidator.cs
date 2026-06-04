using FluentValidation;

namespace OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;

public sealed class ListTrackedPlayersQueryValidator : AbstractValidator<ListTrackedPlayersQuery>
{
    public ListTrackedPlayersQueryValidator()
    {
    }
}
