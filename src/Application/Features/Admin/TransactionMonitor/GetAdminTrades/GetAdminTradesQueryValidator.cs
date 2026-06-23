using FluentValidation;

namespace OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminTrades;

public sealed class GetAdminTradesQueryValidator : AbstractValidator<GetAdminTradesQuery>
{
    public GetAdminTradesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must be on or after 'From'.");
    }
}
