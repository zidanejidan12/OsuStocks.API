using FluentValidation;
using OsuStocks.Domain.Models.Market;

namespace OsuStocks.Application.Features.Market.GetStockCandles;

public sealed class GetStockCandlesQueryValidator : AbstractValidator<GetStockCandlesQuery>
{
    public GetStockCandlesQueryValidator()
    {
        RuleFor(x => x.StockId).NotEmpty();

        RuleFor(x => x.Range)
            .Must(HistoryRangeSpec.IsSupported)
            .WithMessage($"Range must be one of: {string.Join(", ", HistoryRangeSpec.SupportedRanges)}.");
    }
}
