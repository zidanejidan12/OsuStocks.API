using OsuStocks.Domain.Investor.Models;

namespace OsuStocks.Domain.Repositories;

/// <summary>Write-side access to the per-user investor progression aggregate.</summary>
public interface IInvestorProfileRepository
{
    /// <summary>
    /// Atomically adds XP to a user's profile, creating it lazily on first award, and returns the
    /// resulting standing. The increment is applied with a compare-and-swap retry so concurrent
    /// awards for the same user never lose or double-count XP. Persists immediately; non-positive
    /// XP is a no-op (returns <see cref="InvestorXpAwardResult.Skipped"/>).
    /// </summary>
    Task<InvestorXpAwardResult> AddXpAsync(
        Guid userId,
        long xpGain,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
