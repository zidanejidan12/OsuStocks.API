using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.HandleOsuCallback;

public sealed class HandleOsuCallbackCommandHandler(
    IOsuOAuthService osuOAuthService,
    IOsuApiClient osuApiClient,
    IOsuTokenManager osuTokenManager,
    IAppTokenService appTokenService,
    IUserRepository userRepository,
    IWalletRepository walletRepository,
    IWalletTransactionRepository walletTransactionRepository,
    IPortfolioRepository portfolioRepository,
    IApplicationDbContext dbContext)
    : IRequestHandler<HandleOsuCallbackCommand, Result<HandleOsuCallbackResponse>>
{
    private const decimal StartingCredits = 100_000m;

    public async Task<Result<HandleOsuCallbackResponse>> Handle(
        HandleOsuCallbackCommand request,
        CancellationToken cancellationToken)
    {
        var oauthState = await osuTokenManager.ConsumeAuthorizationStateAsync(request.State, cancellationToken);
        if (oauthState is null)
        {
            return Result.Failure<HandleOsuCallbackResponse>("INVALID_STATE", "OAuth state is invalid or expired.");
        }

        try
        {
            var osuToken = await osuOAuthService.ExchangeCodeForTokenAsync(request.Code, cancellationToken);
            var osuUser = await osuApiClient.GetCurrentUserAsync(osuToken.AccessToken, cancellationToken);

            var user = await userRepository.GetByOsuUserIdAsync(osuUser.OsuUserId, cancellationToken);
            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    OsuUserId = osuUser.OsuUserId,
                    Username = osuUser.Username,
                    AvatarUrl = osuUser.AvatarUrl,
                    Role = UserRole.User,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "oauth",
                    LastLoginAt = DateTimeOffset.UtcNow
                };

                await userRepository.AddAsync(user, cancellationToken);

                var wallet = new Wallet
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Balance = StartingCredits,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "oauth"
                };
                await walletRepository.AddAsync(wallet, cancellationToken);

                var initialGrant = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    TransactionType = WalletTransactionType.InitialGrant,
                    Amount = StartingCredits,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await walletTransactionRepository.AddAsync(initialGrant, cancellationToken);

                var portfolio = new Portfolio
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "oauth"
                };
                await portfolioRepository.AddAsync(portfolio, cancellationToken);
            }
            else
            {
                user.Username = osuUser.Username;
                user.AvatarUrl = osuUser.AvatarUrl;
                user.LastLoginAt = DateTimeOffset.UtcNow;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                user.UpdatedBy = "oauth";

                userRepository.Update(user);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await osuTokenManager.SaveUserTokenAsync(user.Id, osuToken, cancellationToken);

            var appToken = appTokenService.CreateToken(user.Id, user.OsuUserId, user.Username, user.Role);
            return Result.Success(new HandleOsuCallbackResponse(appToken.AccessToken, appToken.ExpiresAt, oauthState.ReturnUrl));
        }
        catch (HttpRequestException)
        {
            return Result.Failure<HandleOsuCallbackResponse>("OSU_API_UNAVAILABLE", "Failed to complete osu! OAuth callback.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<HandleOsuCallbackResponse>("OAUTH_PROCESSING_FAILED", ex.Message);
        }
    }
}
