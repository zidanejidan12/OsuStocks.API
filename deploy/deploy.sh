#!/usr/bin/env bash
#
# One-command deploy for OsuStocks on the prod server — replaces the
# "cd ~/OsuStocks.API && git pull; cd ~/OsuStocks.Web && git pull; cd ...; docker compose ..." dance.
#
#   ./deploy/deploy.sh              pull both repos, rebuild + recreate api/worker/web, health-check
#   ./deploy/deploy.sh --migrate    ...and apply EF migrations (use when a merged PR adds one)
#   ./deploy/deploy.sh --no-pull     deploy the current checkout without git pull
#   ./deploy/deploy.sh --caddy       also force-recreate caddy (needed after editing deploy/Caddyfile)
#
# Run from anywhere; it locates the repo from its own path. The web repo is
# expected as a sibling (../OsuStocks.Web); override with WEB_DIR=/path.
set -euo pipefail

API_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_DIR="${WEB_DIR:-$(cd "$API_DIR/.." && pwd)/OsuStocks.Web}"
cd "$API_DIR"
COMPOSE=(docker compose -f docker-compose.prod.yml --env-file .env)

PULL=1; MIGRATE=0; CADDY=0
for arg in "$@"; do
  case "$arg" in
    --no-pull) PULL=0 ;;
    --migrate) MIGRATE=1 ;;
    --caddy)   CADDY=1 ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

if [ "$PULL" -eq 1 ]; then
  echo "==> Pulling OsuStocks.API"
  git pull --ff-only
  if [ -d "$WEB_DIR/.git" ]; then
    echo "==> Pulling OsuStocks.Web ($WEB_DIR)"
    git -C "$WEB_DIR" pull --ff-only
  else
    echo "!! Web repo not found at $WEB_DIR — skipping its pull (set WEB_DIR to override)"
  fi
fi

echo "==> Building images (api, worker, web)"
"${COMPOSE[@]}" build api worker web

if [ "$MIGRATE" -eq 1 ]; then
  echo "==> Applying database migrations"
  set -a; . ./.env; set +a
  docker run --rm --network osustocks_default -v "$PWD":/src -w /src \
    -e ConnectionStrings__Postgres="Host=postgres;Port=5432;Database=osu_stocks;Username=osu_stocks;Password=$POSTGRES_PASSWORD" \
    mcr.microsoft.com/dotnet/sdk:9.0 bash -lc \
    "dotnet tool install --global dotnet-ef >/dev/null 2>&1; export PATH=\$PATH:/root/.dotnet/tools; \
     dotnet restore && dotnet ef database update \
     --project src/Infrastructure/OsuStocks.Infrastructure.csproj \
     --startup-project src/Api/OsuStocks.Api.csproj"
fi

echo "==> Starting/recreating the stack"
"${COMPOSE[@]}" up -d

if [ "$CADDY" -eq 1 ]; then
  # Caddy mounts deploy/Caddyfile as a single file; after git replaces it the inode
  # is stale, so a plain reload re-reads the old config — force a recreate.
  echo "==> Recreating caddy (picks up Caddyfile changes)"
  "${COMPOSE[@]}" up -d --force-recreate caddy
fi

echo "==> Health check"
ok=0
for _ in $(seq 1 10); do
  if curl -fsS -o /dev/null https://api.osustocks.com/health; then ok=1; break; fi
  sleep 3
done
if [ "$ok" -eq 1 ]; then
  echo "OK — https://api.osustocks.com/health is healthy. Deploy complete."
else
  echo "!! Health check failed after ~30s. Check: ${COMPOSE[*]} logs --tail=50 api" >&2
  exit 1
fi
