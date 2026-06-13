using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Investor.Interfaces;
using OsuStocks.Domain.Investor.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class InvestorProfileRepository(
    AppDbContext dbContext,
    IInvestorLevelCalculator levelCalculator) : IInvestorProfileRepository
{
    private const string ActorName = "investor-xp";
    private const int MaxAttempts = 4;

    public async Task<InvestorXpAwardResult> AddXpAsync(
        Guid userId,
        long xpGain,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        if (xpGain <= 0L)
        {
            return InvestorXpAwardResult.Skipped;
        }

        // Compare-and-swap loop: read the current standing, then apply an atomic, guarded update
        // (or insert). ExecuteUpdate runs as a single DB statement and bypasses the change tracker,
        // so the WHERE total_xp == observed guard is the optimistic concurrency check — if another
        // award slipped in, zero rows match and we retry against the fresh value. This keeps the
        // increment exact under concurrency without RowVersion exceptions surfacing to the caller.
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var current = await dbContext.InvestorProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.TotalXp, x.Level })
                .FirstOrDefaultAsync(cancellationToken);

            if (current is null)
            {
                var insertedTotal = xpGain;
                var insertedLevel = levelCalculator.GetProgress(insertedTotal).Level;

                var profile = new InvestorProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TotalXp = insertedTotal,
                    Level = insertedLevel,
                    LastLevelUpAt = insertedLevel > 1 ? occurredAt : null,
                    RowVersion = 1,
                    CreatedAt = occurredAt,
                    CreatedBy = ActorName,
                };

                dbContext.InvestorProfiles.Add(profile);
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return new InvestorXpAwardResult(true, 1, insertedLevel, insertedTotal);
                }
                catch (DbUpdateException)
                {
                    // Lost the create race against a concurrent first award — detach and retry the
                    // update path against the row the other request just inserted.
                    dbContext.Entry(profile).State = EntityState.Detached;
                    continue;
                }
            }

            var previousLevel = current.Level;
            var newTotal = current.TotalXp + xpGain;
            var newLevel = levelCalculator.GetProgress(newTotal).Level;
            var leveledUp = newLevel > previousLevel;
            var observedTotal = current.TotalXp;

            var affected = await dbContext.InvestorProfiles
                .Where(x => x.UserId == userId && x.TotalXp == observedTotal)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.TotalXp, newTotal)
                    .SetProperty(x => x.Level, newLevel)
                    .SetProperty(x => x.LastLevelUpAt, x => leveledUp ? occurredAt : x.LastLevelUpAt)
                    .SetProperty(x => x.UpdatedAt, occurredAt)
                    .SetProperty(x => x.UpdatedBy, ActorName)
                    .SetProperty(x => x.RowVersion, x => x.RowVersion + 1),
                    cancellationToken);

            if (affected == 1)
            {
                return new InvestorXpAwardResult(true, previousLevel, newLevel, newTotal);
            }

            // affected == 0: total_xp moved under us; retry with a fresh read.
        }

        return InvestorXpAwardResult.Skipped;
    }
}
