namespace OsuStocks.Application.Common.Interfaces;

public interface IOAuthReturnUrlPolicy
{
    bool IsAllowed(string? returnUrl);
}
