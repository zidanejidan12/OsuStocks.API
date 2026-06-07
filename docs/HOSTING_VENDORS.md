# Hosting & Vendor Recommendation

> Audience: sponsor / budget owner. Purpose: pick where osu!Stocks runs in production and what it costs per month.
> Pricing checked **June 2026** (sources at the bottom). Treat as indicative — confirm at signup. Hetzner prices are **ex-VAT** in EUR; USD figures are approximate conversions.

---

## TL;DR — recommendation

> **Final decision (2026-06-07):** going with **DigitalOcean — Singapore, 8 GB Droplet** instead of Hetzner. The audience is in **Indonesia** and Hetzner has no Asia-Pacific region (~180–300 ms latency from ID); DO Singapore is ~20–40 ms from Jakarta. Same **Option A** architecture (one VPS + Docker Compose) — only the vendor/region/price change (~$48/mo vs ~$10). The Hetzner-centred analysis below is kept for reference. Actionable plan: [`DEPLOYMENT_PLAN_OPTION_A.md`](DEPLOYMENT_PLAN_OPTION_A.md).

Run the whole stack on **one small VPS** using the Docker Compose setup we already have. Nothing needs to be re-architected.

| | |
|---|---|
| **Recommended host** | Hetzner Cloud **CX32** (4 vCPU / 8 GB / 80 GB) |
| **Domain** | `.com` via Cloudflare / Porkbun / Spaceship |
| **SSL** | Free (Let's Encrypt) |
| **Off-host backups** | Cloudflare R2 or Backblaze B2 (free tier covers us) |
| **Uptime monitoring** | UptimeRobot (free) |
| **≈ Total** | **~$13–15 / month** (≈ **$155–180 / year**) |

This is the cheapest credible production setup. We can move to managed databases or a platform-as-a-service later if reliability needs grow — see the tiers below.

---

## What we're actually hosting

The app is five Docker containers defined in `docker-compose.yml`:

| Container | Role | Resource profile |
|---|---|---|
| `api` | ASP.NET Core API (public) | light–moderate |
| `worker` | Background jobs (sync, market engine) | light, periodic |
| `postgres` | PostgreSQL 16 — **the only durable data** | needs RAM + reliable disk |
| `redis` | Cache / rate-limit / OAuth state | light |
| `nginx` | Reverse proxy + TLS termination | light |

So this is a **single modest server's** worth of work at launch. **8 GB RAM** is the comfortable size (4 GB works but is tight running .NET + Postgres + Redis together). The key risk to protect against is **Postgres data loss**, which the backup plan below handles.

---

## Option tiers

### ⭐ Option A — Single VPS, self-hosted (recommended for launch)
Everything in one VPS via `docker compose up`. Cheapest, matches our current setup exactly.

| Item | Vendor / plan | Monthly |
|---|---|---|
| VPS (4 vCPU / 8 GB / 80 GB) | **Hetzner CX32** | €8.99 ≈ **$9.80** |
| Domain (.com, amortised) | Cloudflare/Porkbun (~$11/yr) | **~$0.90** |
| TLS certificate | Let's Encrypt | **$0** |
| Off-host DB backups | Cloudflare R2 / Backblaze B2 free tier | **~$0** |
| Uptime monitoring | UptimeRobot free | **$0** |
| **Total** | | **≈ $11–13 / month** |

- ✅ Lowest cost, full control, no vendor lock-in, identical to dev setup.
- ⚠️ We run OS patching + the DB ourselves. Mitigated by automated backups (see `OPERATIONS_RUNBOOK.md` §6) and Hetzner's optional auto-snapshots (+20% of server price ≈ $2/mo).
- **Cheaper variant:** Hetzner **CX22** (2 vCPU / 4 GB) at €4.49 ≈ **$4.90/mo** → total **~$6–8/mo**. Fine to start; size up if memory gets tight.

### Option B — VPS + managed PostgreSQL (more resilient)
Keep app on a VPS, move the database to a managed service (automated backups, failover, patching handled for us).

| Item | Vendor / plan | Monthly |
|---|---|---|
| VPS (app + worker + redis + nginx) | Hetzner CX22 | ~$4.90 |
| Managed PostgreSQL (4 GB) | DigitalOcean Managed Postgres | **$60.90** |
| Domain + TLS + monitoring | as above | ~$1 |
| **Total** | | **≈ $67 / month** |

- ✅ Database is someone else's problem (backups, HA, upgrades).
- ⚠️ ~5× the cost of Option A. Worth it only once data/users justify it.

### Option C — Platform-as-a-Service (least ops, no server)
Push containers; the platform runs them. Easiest to operate, priciest per unit.

| Item | Render | Railway |
|---|---|---|
| API (web service) | $7–25 | usage-based |
| Worker (background) | $7+ | usage-based |
| PostgreSQL | from $6 | ~$92 |
| Redis | add-on | add-on |
| **Rough total** | **~$25–50/mo** | **$20–100/mo** (scales with usage) |

- ✅ No Linux/Docker host to maintain; fast to ship; easy scaling.
- ⚠️ More expensive at steady load; some lock-in; need to split our compose into per-service definitions.

---

## Recommended vendor shortlist (by category)

**VPS** — pick one:
| Vendor | Entry plan (≈8 GB) | Monthly | Notes |
|---|---|---|---|
| **Hetzner** ⭐ | CX32 (4 vCPU/8 GB) | €8.99 ≈ $9.80 | Best price/performance; EU + US regions |
| DigitalOcean | 8 GB / 2 vCPU droplet | ~$48 | Polished UX, more expensive |
| Vultr / Linode | 8 GB | ~$40–48 | Comparable to DO |

**Domain registrar** — `.com` ~$9–12/yr, all include free WHOIS privacy:
| Vendor | New | Renewal |
|---|---|---|
| **Cloudflare** ⭐ | at-cost | ~$10.44/yr (transfer-in only) |
| Porkbun | ~$11.08 | ~$11.08 |
| Spaceship | ~$9.08 | ~$10.18 |

**SSL:** Let's Encrypt — **free**, automated via certbot or Caddy. No paid cert needed.

**Off-host backups (critical):** Docker volumes don't survive host loss. Push the daily `pg-backup.sh` dump to object storage:
| Vendor | Free tier | Then |
|---|---|---|
| **Cloudflare R2** ⭐ | 10 GB free, no egress fees | $0.015/GB-mo |
| Backblaze B2 | 10 GB free | $6/TB-mo |

Our DB dump is megabytes → effectively **$0**.

**Uptime monitoring:** **UptimeRobot** (free, 50 monitors) on `https://api.<domain>/health`. Optional: Better Stack free tier for logs + status page.

---

## 12-month cost projection (recommended Option A)

| Line item | Monthly | Year 1 |
|---|---|---|
| Hetzner CX32 VPS | ~$9.80 | ~$118 |
| Domain (.com) | ~$0.90 | ~$11 |
| TLS (Let's Encrypt) | $0 | $0 |
| Off-host backups (R2/B2 free tier) | ~$0 | ~$0 |
| Uptime monitoring (UptimeRobot free) | $0 | $0 |
| *(optional)* Hetzner auto-snapshots | ~$2 | ~$24 |
| **Total** | **~$11–13** | **~$130–155** |

Starting on the **CX22 (4 GB)** instead drops this to **~$6–8/month (~$75–95/year)**.

---

## When to upgrade

- **Memory pressure / slow queries** → bump the VPS one size (Hetzner resize is a few minutes of downtime).
- **Can't afford DB downtime / want hands-off DB** → move to **Option B** (managed Postgres).
- **Don't want to run a server at all** → move to **Option C** (Render/Railway).
- **Traffic growth** → put Cloudflare (free) in front for CDN/DDoS, then scale the VPS or split services.

We can launch on Option A and revisit only when a real constraint appears — no need to pre-pay for scale.

---

## Notes for the sponsor

- These are **infrastructure** costs only. There are no per-user fees; osu! OAuth and Let's Encrypt are free.
- One **annual domain renewal** (~$11) is the only yearly lump sum; everything else is monthly and cancellable.
- Total exposure to start is roughly **a coffee or two per month**. The biggest operational risk (database loss) is covered by free off-host backups.
- Recommendation: **start on Hetzner CX22 or CX32 (Option A)**; it mirrors our current Docker setup exactly, so deployment is `docker compose up` per `OPERATIONS_RUNBOOK.md`.

---

### Sources (verify current pricing at signup)
- [Hetzner Cloud pricing](https://www.hetzner.com/cloud) · [Hetzner CX22 pricing 2026](https://vpsfor.dev/posts/hetzner-cx22-pricing-2026/) · [Hetzner price adjustment (Apr 2026)](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/)
- [DigitalOcean Droplet pricing](https://www.digitalocean.com/pricing/droplets) · [DigitalOcean Managed Databases pricing](https://www.digitalocean.com/pricing/managed-databases)
- [Render pricing](https://render.com/pricing) · [Render vs Railway 2026](https://encore.dev/articles/render-vs-railway)
- [Cheapest domain registrars 2026](https://domaindetails.com/registrars/cheapest)
