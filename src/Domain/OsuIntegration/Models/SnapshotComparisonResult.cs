using OsuStocks.Domain.OsuIntegration.Events;

namespace OsuStocks.Domain.OsuIntegration.Models;

public sealed record SnapshotComparisonResult(
    IReadOnlyCollection<OsuDomainEvent> Events,
    bool IsInactive,
    bool IsRankImproved);
