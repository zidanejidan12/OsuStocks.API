using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Profile.UpdateProfileShowcase;

/// <summary>
/// Sets the player's profile customization: the achievement whose name is shown as their title
/// (<paramref name="EquippedTitleCode"/>, null clears it) and the achievements pinned to their
/// showcase. Both must reference achievements the player has actually unlocked.
/// </summary>
public sealed record UpdateProfileShowcaseCommand(
    Guid UserId,
    string? EquippedTitleCode,
    IReadOnlyList<string> ShowcasedAchievementCodes)
    : IRequest<Result<UpdateProfileShowcaseResponse>>;
