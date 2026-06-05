using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetMarketStocks;

public sealed class GetMarketStocksQueryHandler(IMarketReadRepository marketReadRepository)
    : IRequestHandler<GetMarketStocksQuery, Result<GetMarketStocksResponse>>
{
    public async Task<Result<GetMarketStocksResponse>> Handle(
        GetMarketStocksQuery request,
        CancellationToken cancellationToken)
    {
        var result = await marketReadRepository.GetStocksAsync(
            new MarketStocksQuerySpec(request.Page, request.PageSize, request.Sort, request.Search),
            cancellationToken);

        return Result.Success(new GetMarketStocksResponse(
            result.Items.Select(x => new MarketStockListItemResponse(
                x.StockId,
                x.PlayerName,
                x.CurrentPrice,
                x.Volume,
                x.PriceChange24h)).ToList(),
            request.Page,
            request.PageSize,
            result.TotalCount));
    }
}
