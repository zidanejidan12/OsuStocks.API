using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Achievements.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Profile.UpdateProfileShowcase;

public sealed class UpdateProfileShowcaseCommandHandler(
    IUserRepository userRepository,
    IUserAchievementRepository userAchievementRepository,
    IAchievementCatalog achievementCatalog,
    IApplicationDbContext dbContext)
    : IRequestHandler<UpdateProfileShowcaseCommand, Result<UpdateProfileShowcaseResponse>>
{
    public const int MaxShowcase = 3;

    public async Task<Result<UpdateProfileShowcaseResponse>> Handle(
        UpdateProfileShowcaseCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UpdateProfileShowcaseResponse>("NOT_FOUND", "User not found.");
        }

        var knownCodes = achievementCatalog.All.Select(a => a.Code).ToHashSet();
        var unlockedCodes = (await userAchievementRepository.GetUnlockedAsync(request.UserId, cancellationToken))
            .Select(u => u.AchievementCode)
            .ToHashSet();

        // Normalize: trim, drop blanks, dedupe while preserving the requested order.
        var showcased = request.ShowcasedAchievementCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct()
            .ToList();

        foreach (var code in showcased)
        {
            if (!knownCodes.Contains(code))
            {
                return Result.Failure<UpdateProfileShowcaseResponse>("NOT_FOUND", $"Unknown achievement '{code}'.");
            }

            if (!unlockedCodes.Contains(code))
            {
                return Result.Failure<UpdateProfileShowcaseResponse>(
                    "CONFLICT", $"Achievement '{code}' is not unlocked yet.");
            }
        }

        var equipped = string.IsNullOrWhiteSpace(request.EquippedTitleCode)
            ? null
            : request.EquippedTitleCode.Trim();

        if (equipped is not null)
        {
            if (!knownCodes.Contains(equipped))
            {
                return Result.Failure<UpdateProfileShowcaseResponse>("NOT_FOUND", $"Unknown achievement '{equipped}'.");
            }

            if (!unlockedCodes.Contains(equipped))
            {
                return Result.Failure<UpdateProfileShowcaseResponse>(
                    "CONFLICT", $"Achievement '{equipped}' is not unlocked yet.");
            }
        }

        user.EquippedTitleCode = equipped;
        user.ShowcasedAchievementCodes = showcased;
        userRepository.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var equippedTitle = equipped is null
            ? null
            : achievementCatalog.All.FirstOrDefault(a => a.Code == equipped)?.Name;

        return Result.Success(new UpdateProfileShowcaseResponse(equipped, equippedTitle, showcased));
    }
}
