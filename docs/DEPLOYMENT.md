# Deployment Guide

## Production Security Defaults

The API and Worker now enforce secure production behavior:

- Hangfire dashboard is protected by Admin role authorization.
- Hangfire dashboard access also requires HTTPS outside Development.
- Swagger is disabled by default outside Development.
- JWT metadata requires HTTPS outside Development.
- Required production secrets must be provided via environment variables.

## Required Environment Variables

Set these for production API and Worker processes:

- `ConnectionStrings__Postgres`
- `ConnectionStrings__Redis`
- `OsuOAuth__ClientId`
- `OsuOAuth__ClientSecret`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`

Additional API requirement:

- `OsuOAuth__RedirectUri`

Optional:

- `Security__EnableSwagger=true` to explicitly enable Swagger outside Development.

## Startup Validation Behavior

When `ASPNETCORE_ENVIRONMENT=Production`:

- Startup fails if any required environment variable is missing.
- Startup fails if `ConnectionStrings:Postgres`, `OsuOAuth:ClientSecret`, or `Jwt:SigningKey` are empty or placeholder values.

This fail-fast behavior prevents accidental deployment with insecure defaults.

## OAuth Return URL Allow-List

Configure trusted frontend origins for `GET /api/v1/auth/login?returnUrl=...`:

- `Security:OAuthReturnUrl:AllowedOrigins` accepts absolute origins (scheme + host + optional port).
- Unknown origins are rejected with `400 VALIDATION_ERROR`.
- `localhost` loopback origins are accepted only in `Development`.

## Example (PowerShell)

API:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
$env:ConnectionStrings__Postgres='Host=db;Port=5432;Database=osu_stocks;Username=osu;Password=strong-password'
$env:ConnectionStrings__Redis='redis:6379,password=strong-redis-password'
$env:OsuOAuth__ClientId='your-client-id'
$env:OsuOAuth__ClientSecret='your-client-secret'
$env:OsuOAuth__RedirectUri='https://api.example.com/api/v1/auth/callback'
$env:Jwt__Issuer='osu-stocks-prod'
$env:Jwt__Audience='osu-stocks-client'
$env:Jwt__SigningKey='use-a-long-random-signing-key-at-least-32-characters'
dotnet run --project src/Api/OsuStocks.Api.csproj
```

Worker:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
$env:ConnectionStrings__Postgres='Host=db;Port=5432;Database=osu_stocks;Username=osu;Password=strong-password'
$env:ConnectionStrings__Redis='redis:6379,password=strong-redis-password'
$env:OsuOAuth__ClientId='your-client-id'
$env:OsuOAuth__ClientSecret='your-client-secret'
$env:Jwt__Issuer='osu-stocks-prod'
$env:Jwt__Audience='osu-stocks-client'
$env:Jwt__SigningKey='use-a-long-random-signing-key-at-least-32-characters'
dotnet run --project src/Worker/OsuStocks.Worker.csproj
```

## Operational Notes

- Keep `Security__EnableSwagger` unset in production unless there is a temporary operational need.
- Restrict Admin role assignment carefully; it now governs Hangfire dashboard access.
- Use TLS termination and forward HTTPS correctly so `Request.IsHttps` remains true for dashboard access.

