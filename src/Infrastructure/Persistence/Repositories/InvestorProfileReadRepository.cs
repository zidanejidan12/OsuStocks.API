using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class InvestorProfileReadRepository(AppDbContext dbContext) : IInvestorProfileReadRepository
{
    public async Task<long?> GetTotalXpByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.InvestorProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => (long?)x.TotalXp)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
