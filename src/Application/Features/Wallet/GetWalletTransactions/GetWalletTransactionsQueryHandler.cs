using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Wallet.GetWalletTransactions;

public sealed class GetWalletTransactionsQueryHandler(
    IWalletRepository walletRepository,
    IWalletTransactionRepository walletTransactionRepository)
    : IRequestHandler<GetWalletTransactionsQuery, Result<GetWalletTransactionsResponse>>
{
    public async Task<Result<GetWalletTransactionsResponse>> Handle(
        GetWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var wallet = await walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet is null)
        {
            return Result.Failure<GetWalletTransactionsResponse>("NOT_FOUND", "Wallet not found.");
        }

        var skip = (request.Page - 1) * request.PageSize;
        var transactions = await walletTransactionRepository.GetProjectedByWalletIdAsync(
            wallet.Id,
            skip,
            request.PageSize,
            cancellationToken);

        var items = transactions
            .Select(x => new WalletTransactionItemResponse(
                x.Id,
                x.TransactionType,
                x.Amount,
                x.ReferenceId,
                x.CreatedAt))
            .ToList();

        return Result.Success(new GetWalletTransactionsResponse(items));
    }
}
