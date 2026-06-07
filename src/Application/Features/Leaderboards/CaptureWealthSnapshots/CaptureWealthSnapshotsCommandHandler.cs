using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Leaderboards.CaptureWealthSnapshots;

public sealed class CaptureWealthSnapshotsCommandHandler(
    IWealthSnapshotRepository wealthSnapshotRepository,
    IApplicationDbContext dbContext)
    : IRequestHandler<CaptureWealthSnapshotsCommand, Result<CaptureWealthSnapshotsResponse>>
{
    public async Task<Result<CaptureWealthSnapshotsResponse>> Handle(
        CaptureWealthSnapshotsCommand request,
        CancellationToken cancellationToken)
    {
        var capturedAt = DateTimeOffset.UtcNow;

        var snapshots = await wealthSnapshotRepository.BuildSnapshotsForAllUsersAsync(capturedAt, cancellationToken);

        if (snapshots.Count == 0)
        {
            return Result.Success(new CaptureWealthSnapshotsResponse(0, capturedAt));
        }

        await wealthSnapshotRepository.AddRangeAsync(snapshots, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CaptureWealthSnapshotsResponse(snapshots.Count, capturedAt));
    }
}
