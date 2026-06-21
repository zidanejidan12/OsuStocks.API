using FluentValidation;

namespace OsuStocks.Application.Features.Profile.UpdateProfileShowcase;

public sealed class UpdateProfileShowcaseValidator : AbstractValidator<UpdateProfileShowcaseCommand>
{
    public UpdateProfileShowcaseValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.ShowcasedAchievementCodes)
            .NotNull()
            .Must(codes => codes.Count <= UpdateProfileShowcaseCommandHandler.MaxShowcase)
            .WithMessage($"You can showcase at most {UpdateProfileShowcaseCommandHandler.MaxShowcase} achievements.");
    }
}
