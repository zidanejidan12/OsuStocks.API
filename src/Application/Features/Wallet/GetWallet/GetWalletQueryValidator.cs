using FluentValidation;

namespace OsuStocks.Application.Features.Wallet.GetWallet;

public sealed class GetWalletQueryValidator : AbstractValidator<GetWalletQuery>
{
    public GetWalletQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
