FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Api/OsuStocks.Api.csproj", "src/Api/"]
COPY ["src/Application/OsuStocks.Application.csproj", "src/Application/"]
COPY ["src/Domain/OsuStocks.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/OsuStocks.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Shared/OsuStocks.Shared.csproj", "src/Shared/"]
RUN dotnet restore "src/Api/OsuStocks.Api.csproj"

COPY src/ src/
WORKDIR /src/src/Api
RUN dotnet publish "OsuStocks.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "OsuStocks.Api.dll"]
