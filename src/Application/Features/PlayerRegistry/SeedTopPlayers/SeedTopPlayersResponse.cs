namespace OsuStocks.Application.Features.PlayerRegistry.SeedTopPlayers;

/// <param name="Requested">Target number of top players requested.</param>
/// <param name="Fetched">How many ranking entries were actually read from the osu! API.</param>
/// <param name="Added">Newly tracked players (with a stock) created this run.</param>
/// <param name="Skipped">Entries skipped because the player was already tracked (or invalid).</param>
public sealed record SeedTopPlayersResponse(int Requested, int Fetched, int Added, int Skipped);
