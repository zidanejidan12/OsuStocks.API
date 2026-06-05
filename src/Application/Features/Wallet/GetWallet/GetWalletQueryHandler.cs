using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Wallet.GetWallet;

public sealed class GetWalletQueryHandler(IWalletRepository walletRepository)
    : IRequestHandler<GetWalletQuery, Result<GetWalletResponse>>
{
    public async Task<Result<GetWalletResponse>> Handle(GetWalletQuery request, CancellationToken cancellationToken)
    {
        var wallet = await walletRepository.GetBalanceByUserIdAsync(request.UserId, cancellationToken);
        if (wallet is null)
        {
            return Result.Failure<GetWalletResponse>("NOT_FOUND", "Wallet not found.");
        }

        return Result.Success(new GetWalletResponse(wallet.Balance));
    }
}
