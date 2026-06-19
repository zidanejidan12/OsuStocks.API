using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.PlayerRegistry.SeedTopPlayers;

/// <summary>
/// Bulk-tracks the current top <paramref name="Count"/> osu! standard players from the global
/// performance ranking. Idempotent: players already tracked are skipped, so it is safe to re-run
/// to pick up newly-risen players. Long-running — invoke via the background job, not inline.
/// </summary>
public sealed record SeedTopPlayersCommand(int Count, string? Actor = null)
    : IRequest<Result<SeedTopPlayersResponse>>;
