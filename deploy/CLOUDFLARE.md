# Cloudflare setup (DDoS / WAF / edge) — OsuStocks

The in-repo defenses (per-IP rate limits, Caddy timeouts/body limits, UFW, fail2ban)
protect against a *single* abusive host. They do **not** stop a distributed flood
(botnet) or large volumetric attack — those need an edge layer. Cloudflare's free
tier gives volumetric + L7 DDoS mitigation, a basic WAF, bot filtering, and hides the
origin IP. This is the single highest-value security upgrade for the project.

This is account/DNS work (not code). ~30–60 min. Do it during a quiet window.

## Prerequisites
- A Cloudflare account (free).
- Access to the domain registrar (Porkbun) to change nameservers.

## Steps

### 1. Add the site to Cloudflare
1. Cloudflare dashboard → **Add a site** → `osustocks.com` → **Free** plan.
2. Cloudflare scans existing DNS records. Verify these point at the server IP
   (`167.233.192.157`) and are **Proxied** (orange cloud ON):
   - `osustocks.com` (A, apex)
   - `api` (A)
   - `app` (A)
   - `grafana` (A) — *optional:* you may leave this DNS-only (grey cloud) if you'd
     rather not proxy the dashboard; it's admin-only anyway.
3. Add any missing records to match the current Porkbun zone.

### 2. Switch nameservers at Porkbun
1. Cloudflare shows two assigned nameservers (e.g. `xxx.ns.cloudflare.com`).
2. Porkbun → domain → **Authoritative Nameservers** → replace with Cloudflare's.
3. Wait for propagation (minutes–hours). Cloudflare emails when active.

### 3. TLS mode — IMPORTANT
- Cloudflare SSL/TLS → **Overview** → set encryption mode to **Full (strict)**.
- Caddy keeps serving its real Let's Encrypt cert on the origin, so Full (strict)
  validates end-to-end. **Do not** use "Flexible" (that would make CF↔origin HTTP).
- No Caddyfile change needed — Caddy still terminates TLS on 443.

### 4. Lock the origin to Cloudflare only (closes the bypass hole)
Until this is done, an attacker who knows the IP can skip Cloudflare entirely.
Restrict the firewall so 80/443 only accept Cloudflare's published ranges:

```bash
ssh deploy@167.233.192.157
# Replace the broad 80/443 allow rules with Cloudflare-only:
sudo ufw delete allow 80/tcp
sudo ufw delete allow 443/tcp
for ip in $(curl -s https://www.cloudflare.com/ips-v4); do sudo ufw allow from $ip to any port 80,443 proto tcp; done
for ip in $(curl -s https://www.cloudflare.com/ips-v6); do sudo ufw allow from $ip to any port 80,443 proto tcp; done
sudo ufw reload
```
Also tighten the **Hetzner Cloud Firewall** the same way (or at minimum keep it at
22/80/443). Keep SSH (22) as-is — it is not proxied by Cloudflare.

> **Origin IP already leaked:** the server IP has been in public DNS, so it's in
> historical-DNS databases. The firewall lock above is what actually closes the
> bypass. For belt-and-suspenders you can ask Hetzner for a new IP after cutover.

### 5. Recover the real client IP behind Cloudflare
Cloudflare sends the visitor IP in `CF-Connecting-IP` and appends to
`X-Forwarded-For`. With the CF→Caddy→app chain there are now **two** proxy hops, so
the app's `GetClientIp` (rightmost XFF = the hop Caddy saw = Cloudflare's IP) would
group everyone under a CF edge IP and the per-IP rate limits would misfire.
**Fix:** have Caddy trust Cloudflare and rewrite the client IP. Add to each proxied
site (or globally) in `deploy/Caddyfile`:

```caddy
trusted_proxies static private_ranges 173.245.48.0/20 103.21.244.0/22 ...  # CF ranges
```
or simpler, set the header Caddy forwards so the app keys off `CF-Connecting-IP`.
Ping me to make this Caddyfile change when you're ready to cut over — it must land
**together** with the nameserver switch, and then deploy with `--caddy`.

### 6. Recommended Cloudflare settings (free tier)
- **Security → WAF → Managed rules:** enable the free managed ruleset.
- **Security → Bots:** enable **Bot Fight Mode**.
- **Security → DDoS:** on by default (HTTP DDoS managed ruleset) — leave it.
- **Rules → Rate limiting rules:** add one free rule, e.g. limit
  `/api/v1/auth/*` to ~20 req/min per IP at the edge (defense in depth with the app).
- **Caching:** cache static assets (`/_next/static/*`, images) to offload the origin.
- **Speed → Always Use HTTPS:** on.

## Verify after cutover
- `dig osustocks.com` → returns Cloudflare IPs (not the origin).
- Site + login still work end-to-end.
- Direct origin hit is blocked: `curl --resolve osustocks.com:443:167.233.192.157 https://osustocks.com` should time out / be refused (proves the firewall lock works).
- Rate limits still key off the real visitor IP (check Grafana / logs), not a CF edge IP.
