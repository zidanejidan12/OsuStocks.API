using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Wallet.GetWalletTransactions;

public sealed record GetWalletTransactionsQuery(Guid UserId, int Page, int PageSize)
    : IRequest<Result<GetWalletTransactionsResponse>>;
