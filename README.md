# omarchylockscreens.com

The community side of the [Lock Screen Explorer](https://github.com/SirJul1337/omarchy-lock-explorer)
plugin: a portal where people submit their own Omarchy lock screen designs, get
them scanned and human-reviewed, and where the plugin's registry is served
from. ASP.NET Core 10 Razor Pages + EF Core (SQLite), GitHub sign-in only.

## Run locally

```
dotnet run
```

Then open <http://localhost:5000> (or the URL it prints). In Development it
seeds sample designs and exposes `/dev/login?user=NAME&admin=true` so every
flow is testable without a real GitHub OAuth app.

## What's here

| Area | Files |
|---|---|
| Data model | `Models/Entities.cs`, `Data/AppDbContext.cs`, `Data/SeedData.cs` |
| GitHub fetch + pin-to-commit | `Services/GitHubContentService.cs` |
| The QML capability scanner | `Services/ScanService.cs` |
| Pages | `Pages/` — Index, Designs (gallery + detail), Submit, Mine, Review |
| Registry endpoint + auth + rate limiting | `Program.cs` |
| Theme system (ported from omarchy-site-concept) | `wwwroot/css/site.css`, `wwwroot/js/theme.js` |

## Registry contract

`GET /api/v1/registry.json` — the endpoint the plugin fetches. Approved designs
only, `Cache-Control: public, max-age=300, stale-while-revalidate=3600`, ETag +
304 support. See `docs/community/plugin-plan.md` in the plugin repo for the
consuming side.

## Deploying

See [DEPLOY.md](DEPLOY.md) — self-contained win-x86 to Simply.com, plus the
Cloudflare free-tier CDN setup.

## The safety model

1. Every submission is pinned to a full commit SHA; per-file SHA-256 is stored
   and re-verified by the plugin on install.
2. An automated scanner (`ScanService`) enforces a QML capability allowlist —
   only QtQuick/QtMultimedia/theme imports, no `Process`, network, `eval` or
   file access — plus size caps (QML 64 KB, video 50 MB).
3. Nothing goes live without a human clicking Approve in `/review`; a Fail scan
   disables the Approve button.
4. Community reports auto-unlist a design at a threshold, pending re-review;
   revoking drops it from the registry within the cache window.
5. Reads are anonymous + CDN-cached; writes require a GitHub account with
   per-account quotas. No secrets ship in the open-source plugin.
