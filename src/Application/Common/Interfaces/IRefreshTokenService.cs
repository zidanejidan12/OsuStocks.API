namespace OsuStocks.Application.Common.Interfaces;

/// <summary>
/// Issues and rotates opaque, long-lived refresh tokens so the SPA can mint fresh short-lived access
/// JWTs without sending the user back through the full osu! OAuth handshake. Tokens are single-use:
/// validating one consumes it and returns a replacement (rotation), which limits the blast radius of
/// a leaked token.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Creates a new refresh token bound to <paramref name="userId"/>.</summary>
    Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates <paramref name="refreshToken"/>, consumes it, and (when valid) issues a replacement.
    /// Returns <c>null</c> if the token is unknown, already used, or expired.
    /// </summary>
    Task<RefreshTokenRotation?> ValidateAndRotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}

/// <summary>A freshly issued refresh token and the instant it expires.</summary>
public sealed record RefreshTokenResult(string Token, DateTimeOffset ExpiresAt);

/// <summary>The owning user plus the rotated replacement token returned from a successful validation.</summary>
public sealed record RefreshTokenRotation(Guid UserId, string Token, DateTimeOffset ExpiresAt);
