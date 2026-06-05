using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Wallet.GetWallet;

public sealed record GetWalletQuery(Guid UserId)
    : IRequest<Result<GetWalletResponse>>;
