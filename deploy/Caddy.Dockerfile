# deploy/Caddy.Dockerfile — Caddy with the edge-hardening plugins baked in.
#
# Stock `caddy:2-alpine` has no rate limiter and no WAF, so we build a custom binary with xcaddy:
#   - caddy-ratelimit  : the `rate_limit` directive (global per-IP request cap)
#   - coraza-caddy      : the `coraza_waf` directive (OWASP-style WAF engine)
#   - coraza-coreruleset: bundles the OWASP Core Rule Set so the @owasp_crs/@coraza includes resolve
#
# Body-size limits and bot/User-Agent filtering need no plugin (Caddy core), see deploy/Caddyfile.
# Rebuilt on the server via `deploy/deploy.sh --caddy` (which now passes --build).
#
# If a build ever fails on a plugin version, pin/adjust the module versions below and rebuild.
FROM caddy:2-builder AS builder

RUN xcaddy build \
    --with github.com/mholt/caddy-ratelimit \
    --with github.com/corazawaf/coraza-caddy/v2 \
    --with github.com/corazawaf/coraza-coreruleset/v4

FROM caddy:2-alpine
COPY --from=builder /usr/bin/caddy /usr/bin/caddy
