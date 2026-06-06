FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Worker/OsuStocks.Worker.csproj", "src/Worker/"]
COPY ["src/Application/OsuStocks.Application.csproj", "src/Application/"]
COPY ["src/Domain/OsuStocks.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/OsuStocks.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Shared/OsuStocks.Shared.csproj", "src/Shared/"]
RUN dotnet restore "src/Worker/OsuStocks.Worker.csproj"

COPY src/ src/
WORKDIR /src/src/Worker
RUN dotnet publish "OsuStocks.Worker.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OsuStocks.Worker.dll"]
