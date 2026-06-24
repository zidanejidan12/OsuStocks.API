#!/usr/bin/env bash
#
# One-command deploy for OsuStocks on the prod server — replaces the
# "cd ~/OsuStocks.API && git pull; cd ~/OsuStocks.Web && git pull; cd ...; docker compose ..." dance.
#
#   ./deploy/deploy.sh              pull both repos, rebuild + recreate api/worker/web, health-check
#   ./deploy/deploy.sh --migrate    ...and apply EF migrations (use when a merged PR adds one)
#   ./deploy/deploy.sh --no-pull     deploy the current checkout without git pull
#   ./deploy/deploy.sh --caddy       also rebuild + recreate caddy (needed after editing
#                                    deploy/Caddyfile or deploy/Caddy.Dockerfile)
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
    echo "!! Web repo not found at $WEB_DIR — the web build would use a stale/missing context. Aborting (set WEB_DIR to override)." >&2
    exit 1
  fi
fi

echo "==> Building images (api, worker, web)"
"${COMPOSE[@]}" build api worker web

if [ "$MIGRATE" -eq 1 ]; then
  echo "==> Applying database migrations"
  if [ ! -f ./.env ]; then
    echo "!! ./.env not found — required for migrations (POSTGRES_PASSWORD). Aborting." >&2
    exit 1
  fi
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
  # Caddy is now a custom build (rate-limit + WAF plugins), so rebuild it. The Caddyfile is a
  # bind-mounted single file; after git replaces it the inode is stale, so a plain reload re-reads
  # the old config. --build picks up Dockerfile/plugin changes and --force-recreate the new config.
  echo "==> Rebuilding + recreating caddy (picks up Caddyfile / Caddy.Dockerfile changes)"
  "${COMPOSE[@]}" build caddy
  "${COMPOSE[@]}" up -d --force-recreate caddy
fi

echo "==> Health check"
check_url() {
  # $1 = label, $2 = url; retries ~30s like the original API check.
  local label="$1" url="$2"
  for _ in $(seq 1 10); do
    if curl -fsS -o /dev/null "$url"; then
      echo "OK — $url ($label) is healthy."
      return 0
    fi
    sleep 3
  done
  echo "!! Health check failed for $label ($url) after ~30s." >&2
  return 1
}

if ! check_url "api" https://api.osustocks.com/health; then
  echo "!! Check: ${COMPOSE[*]} logs --tail=50 api" >&2
  exit 1
fi
if ! check_url "frontend" https://osustocks.com/; then
  echo "!! Check: ${COMPOSE[*]} logs --tail=50 web" >&2
  exit 1
fi
if ! check_url "grafana" https://grafana.osustocks.com/; then
  echo "!! Check: ${COMPOSE[*]} logs --tail=50 grafana caddy" >&2
  exit 1
fi
echo "Deploy complete."
