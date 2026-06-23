using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminWalletTransactions;

public sealed record GetAdminWalletTransactionsQuery(
    Guid? UserId = null,
    WalletTransactionType? TransactionType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 25) : IRequest<Result<GetAdminWalletTransactionsResponse>>;
