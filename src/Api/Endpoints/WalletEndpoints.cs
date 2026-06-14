using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Wallet.GetWallet;
using OsuStocks.Application.Features.Wallet.GetWalletTransactions;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class WalletEndpoints
{
    public static void MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        var walletGroup = app.MapGroup("/api/v1/wallet")
            .RequireAuthorization()
            .WithTags("Wallet");

        walletGroup.MapGet("", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetWalletQuery(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { balance = result.Value.Balance });
        });

        walletGroup.MapGet("/transactions", async (
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetWalletTransactionsQuery(userId, page ?? 1, pageSize ?? 25), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { items = result.Value.Items });
        });
    }
}
