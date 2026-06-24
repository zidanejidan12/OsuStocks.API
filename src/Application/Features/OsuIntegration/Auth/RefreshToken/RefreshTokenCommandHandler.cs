using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.RefreshToken;

/// <summary>
/// Exchanges a valid refresh token for a fresh access JWT plus a rotated refresh token, so an active
/// session is renewed silently without another osu! OAuth round-trip. The access JWT stays short-lived;
/// the (single-use, rotating) refresh token is what carries the long session.
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IRefreshTokenService refreshTokenService,
    IUserRepository userRepository,
    IAppTokenService appTokenService)
    : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    public async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var rotation = await refreshTokenService.ValidateAndRotateAsync(request.RefreshToken, cancellationToken);
        if (rotation is null)
        {
            return Result.Failure<RefreshTokenResponse>(
                "INVALID_REFRESH_TOKEN",
                "The refresh token is invalid, already used, or expired.");
        }

        var user = await userRepository.GetByIdAsync(rotation.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<RefreshTokenResponse>(
                "INVALID_REFRESH_TOKEN",
                "The refresh token is invalid, already used, or expired.");
        }

        var appToken = appTokenService.CreateToken(user.Id, user.OsuUserId, user.Username, user.Role);

        return Result.Success(new RefreshTokenResponse(
            appToken.AccessToken,
            appToken.ExpiresAt,
            rotation.Token,
            rotation.ExpiresAt));
    }
}
