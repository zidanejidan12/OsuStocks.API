namespace OsuStocks.Domain.Repositories;

/// <summary>Read-side access to investor progression.</summary>
public interface IInvestorProfileReadRepository
{
    /// <summary>
    /// Returns the lifetime XP for a user, or null when the user has no profile yet
    /// (i.e. has never traded). Callers treat null as 0 XP (level 1).
    /// </summary>
    Task<long?> GetTotalXpByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
