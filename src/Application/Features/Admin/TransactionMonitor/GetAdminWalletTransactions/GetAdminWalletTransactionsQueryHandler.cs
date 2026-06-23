using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminWalletTransactions;

public sealed class GetAdminWalletTransactionsQueryHandler(IAdminTransactionReadRepository repository)
    : IRequestHandler<GetAdminWalletTransactionsQuery, Result<GetAdminWalletTransactionsResponse>>
{
    public async Task<Result<GetAdminWalletTransactionsResponse>> Handle(
        GetAdminWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;

        var (transactions, totalCount) = await repository.GetWalletTransactionsAsync(
            request.UserId,
            request.TransactionType,
            request.From,
            request.To,
            skip,
            request.PageSize,
            cancellationToken);

        var items = transactions
            .Select(x => new AdminWalletTransactionItemResponse(
                x.Id,
                x.UserId,
                x.Username,
                x.TransactionType.ToString(),
                x.Amount,
                x.ReferenceId,
                x.CreatedAt))
            .ToList();

        return Result.Success(new GetAdminWalletTransactionsResponse(items, totalCount, request.Page, request.PageSize));
    }
}
