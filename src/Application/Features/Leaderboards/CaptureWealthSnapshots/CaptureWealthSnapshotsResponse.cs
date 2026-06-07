namespace OsuStocks.Application.Features.Leaderboards.CaptureWealthSnapshots;

public sealed record CaptureWealthSnapshotsResponse(
    int SnapshotsCaptured,
    DateTimeOffset CapturedAt);
