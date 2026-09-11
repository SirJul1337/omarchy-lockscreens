# omarchycommunity.org

The community side of the [Lock Screen Explorer](https://github.com/SirJul1337/omarchy-lock-explorer)
plugin: where people submit their own Omarchy lock screen designs, where anyone
can browse them with a preview, and where the plugin's registry is served from.

ASP.NET Core Razor Pages + EF Core — MySQL in production, SQLite locally.

## Submitting a design is a pull request

Against **this** repository, adding one folder:

```
Designs/<id>/
  design.json     name, author, description, tags
  YourDesign.qml
  preview.png     (or .jpg)
```

[check design](.github/workflows/pr-check.yml) runs `scripts/scan.py` over it,
a maintainer reads the QML and merges, and
[publish the index](.github/workflows/publish.yml) regenerates `index.json`.
Full instructions in [CONTRIBUTING.md](CONTRIBUTING.md) — or press **S** in the
plugin's Community tab and it will do the mechanical half for you.

## How the two halves fit

**The repository owns the files.** `Designs/` holds exactly what a person wrote
and a person reviewed, so the files that are reviewed are the files that ship.

**The database owns what changes afterwards** — likes, installs, whether a
design is still listed, and the SHA-256 each file was approved at.

`index.json` is the handover, and `Services/RegistrySync.cs` is what crosses
it: it polls the index, and reconciles. An id it has not seen is an insert, a
file whose `sha256` moved is a new version, an id that has gone is a
withdrawal. It mirrors the files into `wwwroot/designs/` as they land, checking
every hash on the way in, and writes the database row only once the files are
there — so the registry never advertises something the site cannot serve.

Reconciling rather than being pushed to is the point: a sync that fails or is
missed is repaired by the next one, where a webhook would quietly lose a design
the first time a delivery failed. And because the index is fetched from GitHub
rather than read from the deployed copy, a merged design goes live on the next
poll without redeploying the site.

## Run locally

```
dotnet run
```

In Development, `Registry:IndexUrl` points at this working tree, so a design
added under `Designs/` syncs without pushing anything. `/dev/login?user=NAME`
stands in for GitHub OAuth, and `/admin/sync` forces a sync rather than waiting
for the timer.

## What's here

| Area | Files |
|---|---|
| Designs, as reviewed | `Designs/`, `scripts/scan.py`, `scripts/build-index.py` |
| Repository → database | `Services/RegistrySync.cs` |
| Data model | `Models/Entities.cs`, `Data/AppDbContext.cs`, `Data/SeedData.cs` |
| Pages | `Pages/` — Index, Designs (gallery + detail), Apply, Review |
| Registry endpoint, MIME, rate limiting | `Program.cs` |
| Wordmark, pixel field, theming | `scripts/build-wordmark.py`, `wwwroot/js/`, `wwwroot/css/` |

## Registry contract

`GET /api/v1/registry.json` — what the plugin fetches. Approved designs only,
absolute URLs, `Cache-Control: public, max-age=300, stale-while-revalidate=3600`,
ETag + 304. The plugin re-checks size and SHA-256 after downloading, then runs
the same scan locally before loading anything, so neither this site nor the
repository is on its own enough to put code on someone's lock screen.

## Still to settle

- **Likes** are in the schema and the payload but nothing writes to them yet —
  it needs either GitHub sign-in or 👍 reactions as the identity check.
- **The older submit-through-the-site flow** (`Pages/Apply`, `Pages/Review`,
  `Services/GitHubContentService.cs`, `Services/ScanService.cs`) predates the
  pull request flow and is a second way in. One of the two should go.

## Licence

MIT, see [LICENSE](LICENSE). Designs are contributed under the same licence.
The vendored ttfx engine under `wwwroot/ttfx/` is MIT as well, with its
attribution in `wwwroot/ttfx/NOTICE`.

## Deploying

See [DEPLOY.md](DEPLOY.md) — self-contained win-x86 to Simply.com, plus the
Cloudflare free-tier setup.
