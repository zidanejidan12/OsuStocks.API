using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.OsuIntegration.Interfaces;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetOsuLoginUrl;

public sealed class GetOsuLoginUrlQueryHandler(IOsuOAuthService osuOAuthService, IOsuTokenManager osuTokenManager)
    : IRequestHandler<GetOsuLoginUrlQuery, Result<GetOsuLoginUrlResponse>>
{
    public async Task<Result<GetOsuLoginUrlResponse>> Handle(
        GetOsuLoginUrlQuery request,
        CancellationToken cancellationToken)
    {
        var state = Guid.NewGuid().ToString("N");
        await osuTokenManager.StoreAuthorizationStateAsync(
            state,
            request.ReturnUrl,
            TimeSpan.FromMinutes(10),
            cancellationToken);

        var authorizationUrl = osuOAuthService.BuildAuthorizationUrl(state);
        return Result.Success(new GetOsuLoginUrlResponse(authorizationUrl));
    }
}
