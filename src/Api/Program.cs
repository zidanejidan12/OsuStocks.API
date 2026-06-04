using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OsuStocks.Api.Common;
using OsuStocks.Application;
using OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;
using OsuStocks.Application.Features.OsuIntegration.Auth.GetOsuLoginUrl;
using OsuStocks.Application.Features.OsuIntegration.Auth.HandleOsuCallback;
using OsuStocks.Infrastructure;
using OsuStocks.Infrastructure.Authentication;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured and contain at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OsuStocks API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {your JWT token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "OsuStocks API v1");
    options.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "Healthy" }));

var authGroup = app.MapGroup("/api/v1/auth");

authGroup.MapGet("/login", async (
    string? returnUrl,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetOsuLoginUrlQuery(returnUrl), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Redirect(result.Value.AuthorizationUrl);
});

authGroup.MapGet("/callback", async (
    string code,
    string state,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new HandleOsuCallbackCommand(code, state), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        accessToken = result.Value.AccessToken,
        expiresAt = result.Value.ExpiresAt,
        returnUrl = result.Value.ReturnUrl
    });
});

authGroup.MapGet("/me", async (
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userIdValue, out var userId))
    {
        return Results.Json(new
        {
            code = "UNAUTHORIZED",
            message = "Authentication token is missing a valid user identifier.",
            traceId = httpContext.TraceIdentifier
        }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await sender.Send(new GetCurrentUserProfileQuery(userId), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        userId = result.Value.UserId,
        osuUserId = result.Value.OsuUserId,
        username = result.Value.Username,
        role = result.Value.Role
    });
})
.RequireAuthorization();

app.MapHangfireDashboard("/hangfire");

app.Run();
