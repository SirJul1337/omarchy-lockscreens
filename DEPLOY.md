# Deploying omarchycommunity.org to Simply.com

This is an ASP.NET Core 10 Razor Pages app, published **self-contained for
win-x86** so Simply.com's shared Windows hosting can run it without any .NET
install on the server (their documented path — support articles 361 and 828).

## 1. What to upload

Everything in the **`publish/`** folder (produced by the command below) goes
into your webhotel's `public_html` for omarchycommunity.org. Rebuild it any
time with:

```
dotnet publish -c Release -r win-x86 --self-contained true -o .\publish
```

The included `web.config` sets **hostingModel="OutOfProcess"**, which Simply's
shared app pool requires, and the project preserves it as-is on publish
(`IsTransformWebConfigDisabled`), so `publish/web.config` is already correct —
no manual step. Upload the folder with the FTP/SFTP credentials from your Simply
control panel (or Web Deploy from Visual Studio — Deployment Mode
*Self-contained*, Target Runtime *win-x86*).

The app self-creates its SQLite database at `App_Data/lockscreens.db` on first
run. That folder has full write access on Simply (support article 261). **Do
not upload a local `App_Data`** — let production start empty. Back the file up
periodically; it holds every design, vote and report.

## 2. Secrets — set these on the server, never in the repo

Create/edit **`appsettings.Production.json`** in the site root on the server
(it is not in source control) and fill in:

```json
{
  "Admins": [ "SirJul1337" ],
  "GitHub": {
    "ClientId": "<from your GitHub OAuth app>",
    "ClientSecret": "<from your GitHub OAuth app>",
    "ApiToken": "<optional PAT, raises the GitHub API rate limit>"
  }
}
```

- **Admins** is the list of GitHub logins that can reach `/review`.
- **GitHub:ApiToken** is optional. Without it the site uses GitHub's anonymous
  API limit (60/hour) to resolve commits — fine at low volume. A classic PAT
  with **no scopes** (public read only) raises it to 5000/hour.

## 3. GitHub OAuth app (for sign-in)

At <https://github.com/settings/developers> → New OAuth App:

- Homepage URL: `https://omarchycommunity.org`
- Authorization callback URL: `https://omarchycommunity.org/signin-github`

Copy the Client ID and generate a Client Secret into
`appsettings.Production.json` as above. The app requests **no scopes** — only
the public profile (login + avatar), nothing else.

Until these are set, sign-in shows a "not configured yet" message and the
public pages + registry still work. (Locally, `dotnet run` in Development also
exposes `/dev/login?user=NAME` so the flows can be tested without OAuth.)

## 4. The CDN — Cloudflare free plan (nothing to buy)

The registry endpoint and static assets already send
`Cache-Control: public, max-age=300, stale-while-revalidate=3600` and an ETag,
so a CDN protects the origin with **zero app changes**. Simply.com has no free
CDN of its own (its Varnish/Redis caching is on Pro/Enterprise plans), so use
Cloudflare's free tier:

1. Add `omarchycommunity.org` to a **free** Cloudflare account.
2. Change the domain's nameservers (in the Simply control panel) to the two
   Cloudflare gives you. DNS stays free.
3. Point an **A record** for `@` (and `www`) at your Simply webhotel's IP,
   **proxied** (orange cloud on).
4. Cloudflare honours the app's `Cache-Control` automatically. Optionally add a
   Cache Rule for `omarchycommunity.org/api/v1/*` → *Eligible for cache* to be
   explicit. That's it — reads now hit Cloudflare's edge, not Simply.
5. **Lock the origin** so nobody bypasses the cache/WAF: once traffic flows
   through Cloudflare, restrict the Simply site to Cloudflare's published IP
   ranges (or use a Cloudflare Tunnel). Optional but recommended.

You can't be "spammed" on reads after this: a cached GET never reaches Simply,
and Cloudflare's free tier absorbs floods. Writes are a different lever — see
below.

**If you skip the CDN entirely**, the site still holds up: the cache headers +
conditional GETs from the plugin keep origin load low, and the site failing
briefly is harmless because the plugin fails open on its cached copy.

## 5. Why it can't be abused (recap, since the plugin is open source)

- **Reads** (`/api/v1/registry.json`, previews) are anonymous and cached — the
  CDN serves them, so volume can't hurt the origin. No API key is shipped in
  the plugin (it would be public within minutes and buy nothing).
- **Writes** (vote, report, submit) all require a GitHub sign-in, with
  per-account quotas (submits/day, reports/day, one vote per design) enforced
  in the app. Spamming writes costs the attacker GitHub accounts, not you.
- **Per-IP backstop**: a 120 req/min sliding window. Behind Cloudflare the real
  client IP is in `CF-Connecting-IP`/`X-Forwarded-For`; to partition the limiter
  on it, add Cloudflare's IP ranges to `KnownProxies` in `Program.cs` (left open
  by default so it degrades to a global limiter rather than trusting a spoofable
  header). The CDN cache and the OAuth gate are the real defenses; this is only
  a backstop.
- **Revocation**: revoking or auto-unlisting a design drops it from
  `registry.json` immediately; with the 5-minute cache TTL the plugin stops
  offering it within minutes.

## 6. Health check after deploy

- `https://omarchycommunity.org/` — landing page renders.
- `https://omarchycommunity.org/api/v1/registry.json` — returns JSON with the
  cache header (empty `designs` array until something is approved).
- Sign in with GitHub, submit a design, approve it at `/review`, confirm it
  appears in the registry within the cache window.
