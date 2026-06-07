using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Leaderboards.CaptureWealthSnapshots;

public sealed record CaptureWealthSnapshotsCommand
    : IRequest<Result<CaptureWealthSnapshotsResponse>>;
