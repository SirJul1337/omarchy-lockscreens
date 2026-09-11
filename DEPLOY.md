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

Locally the app keeps a SQLite database under `App_Data/`; **production uses
the MySQL database from your Simply control panel** (section 2). Either way,
**do not upload a local `App_Data`** — it is developer data and production
creates its own schema on first run.

The designs themselves are not in the database and are not uploaded: the
registry sync fetches them from the repository and mirrors them into
`wwwroot/designs/` on the server. So a merged design goes live on the next
poll without a redeploy, and an empty gallery right after deploying just means
the first sync has not run yet (it runs ten seconds after start, then every
`Registry:SyncMinutes`).

## 2. Configuration — set this on the server, never in the repo

Create/edit **`appsettings.Production.json`** in the site root on the server
(it is not in source control):

```json
{
  "ConnectionStrings": {
    "MySql": "Server=...;Port=3306;Database=...;User ID=...;Password=...;SslMode=Preferred;"
  },
  "Admin": { "Key": "<a long random string>" }
}
```

- **ConnectionStrings:MySql** comes from the Simply control panel.
- **Admin:Key** is the single maintainer login, used at `/admin/login` to reach
  `/admin/sync`. The shipped default in `appsettings.json` is **empty on
  purpose**: with no key set, every login is refused rather than a guessable
  one being accepted. So if you do not set this, there is no admin — which is
  safe, just inconvenient.

`Registry:IndexUrl` and `Registry:RepoUrl` are already set in
`appsettings.json` and need nothing on the server. Note the index is fetched
anonymously from `raw.githubusercontent.com`, so **the repository has to be
public** for the sync to work.

There is no OAuth and there are no user accounts. Nobody signs in but you, and
designs arrive by pull request rather than through the site.

## 3. The CDN — Cloudflare free plan (nothing to buy)

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

## 4. Why it can't be abused (recap, since everything is public)

- **Reads** (`/api/v1/registry.json`, previews) are anonymous and cached — the
  CDN serves them, so volume can't hurt the origin. No API key is shipped in
  the plugin (it would be public within minutes and buy nothing).
- **There is no public write path at all.** Submitting is a pull request on
  GitHub, so review, rate limiting and abuse handling are GitHub's problem
  rather than something running here. The only authenticated endpoint is
  `/admin/sync`, behind the single admin key.
- **Per-IP backstop**: a 120 req/min sliding window. Behind Cloudflare the real
  client IP is in `CF-Connecting-IP`/`X-Forwarded-For`; to partition the limiter
  on it, add Cloudflare's IP ranges to `KnownProxies` in `Program.cs` (left open
  by default so it degrades to a global limiter rather than trusting a spoofable
  header). The CDN cache and having no write path are the real defenses; this
  is only a backstop.
- **Revocation**: delete the design's folder from the repository. The next
  sync notices it has gone, unlists it, and it leaves `registry.json` within
  the cache window.

## 5. Health check after deploy

- `https://omarchycommunity.org/` — landing page renders.
- `https://omarchycommunity.org/api/v1/registry.json` — returns JSON with the
  cache header (empty `designs` array until something is approved).
- `https://omarchycommunity.org/designs` — the designs listed, each with its
  preview. If it is empty, check the log for the sync: an unreachable or
  unparseable index leaves the database untouched rather than emptying it.
- Merge a design into `Designs/`, wait for the index workflow, and confirm it
  appears within `Registry:SyncMinutes`.
