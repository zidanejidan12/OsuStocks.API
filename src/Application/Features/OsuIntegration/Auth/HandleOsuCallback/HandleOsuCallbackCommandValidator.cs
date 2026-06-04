using FluentValidation;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.HandleOsuCallback;

public sealed class HandleOsuCallbackCommandValidator : AbstractValidator<HandleOsuCallbackCommand>
{
    public HandleOsuCallbackCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.State)
            .NotEmpty()
            .MaximumLength(128);
    }
}
