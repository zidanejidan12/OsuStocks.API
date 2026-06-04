using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface IAppTokenService
{
    AppAuthToken CreateToken(Guid userId, long osuUserId, string username, UserRole role);
}
