using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Application.Features.Trading.Services;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetTradeQuote;

public sealed class GetTradeQuoteQueryHandler(
    IPlayerStockRepository playerStockRepository,
    ILiquidityProvider liquidityProvider,
    IMarketEventProcessingService marketEventProcessingService,
    ITradeFeePolicy tradeFeePolicy)
    : IRequestHandler<GetTradeQuoteQuery, Result<GetTradeQuoteResponse>>
{
    public async Task<Result<GetTradeQuoteResponse>> Handle(
        GetTradeQuoteQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await playerStockRepository.GetByIdAsync(request.StockId, cancellationToken);
        if (stock is null)
        {
            return Result.Failure<GetTradeQuoteResponse>("NOT_FOUND", "Stock not found.");
        }

        // Mirror the exact trade pipeline (read-only): liquidity-scaled price move → average-fill
        // slippage (mid) ± half the bid/ask spread → progressive fee on the gross. No persistence.
        var liquidity = await liquidityProvider.GetLiquidityAsync(stock.Id, cancellationToken);
        var input = request.IsSell
            ? MarketPriceInput.Sell(request.Quantity, liquidity)
            : MarketPriceInput.Buy(request.Quantity, liquidity);

        var preview = await marketEventProcessingService.PreviewAsync(stock.CurrentPrice, input, cancellationToken);
        var mid = (preview.PreviousPrice + preview.NewPrice) / 2m;
        var unitPrice = request.IsSell
            ? mid * (1m - preview.SpreadRate / 2m)
            : mid * (1m + preview.SpreadRate / 2m);
        var gross = unitPrice * request.Quantity;
        var fee = await tradeFeePolicy.ComputeFeeAsync(gross, cancellationToken);
        var total = request.IsSell ? gross - fee : gross + fee;

        return Result.Success(new GetTradeQuoteResponse(
            request.Quantity,
            decimal.Round(unitPrice, 4),
            decimal.Round(gross, 2),
            decimal.Round(fee, 2),
            decimal.Round(total, 2),
            preview.NewPrice,
            request.IsSell));
    }
}
