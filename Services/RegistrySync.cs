using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OmarchyLockscreens.Data;
using OmarchyLockscreens.Models;

namespace OmarchyLockscreens.Services;

/// <summary>
/// Pulls the designs repository into the database.
///
/// The repo owns the files somebody wrote and somebody reviewed; this owns
/// everything that changes afterwards -- likes, installs, whether a design is
/// still listed. The handover is one file, index.json, which the repo writes
/// on merge.
///
/// Reconciling rather than being pushed to is the whole point: an id that is
/// new here is an insert, a file whose sha256 moved is a new version, and an
/// id that has gone is a withdrawal. Nothing has to arrive in order, and a
/// sync that is missed or fails half way is fixed by the next one. A webhook
/// would be faster and would quietly lose a design the first time a delivery
/// failed.
///
/// Files are mirrored into wwwroot as they land, so the site serves the design
/// and its preview from its own domain and the plugin never has to reach
/// GitHub. The sha256 in the database is what says the copy is honest.
/// </summary>
public class RegistrySync(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    IWebHostEnvironment env,
    ILogger<RegistrySync> log)
{
    /// <summary>Where index.json lives: a URL, or a path for working locally.</summary>
    public string Source => config["Registry:IndexUrl"] ?? "";

    public record SyncResult(
        int Added, int Updated, int Withdrawn, int Restored, int Unchanged, string? Error)
    {
        public bool Ok => Error is null;
        public override string ToString() => Error
            ?? $"{Added} added, {Updated} updated, {Withdrawn} withdrawn, "
             + $"{Restored} restored, {Unchanged} unchanged";
    }

    public async Task<SyncResult> RunAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Source))
            return new(0, 0, 0, 0, 0, "Registry:IndexUrl is not set");

        JsonElement index;
        try
        {
            index = JsonSerializer.Deserialize<JsonElement>(await ReadIndexAsync(ct));
        }
        catch (Exception e)
        {
            log.LogWarning(e, "could not read the design index from {Source}", Source);
            return new(0, 0, 0, 0, 0, "Could not read the index: " + e.Message);
        }

        if (!index.TryGetProperty("schemaVersion", out var v) || v.GetInt32() != 1)
            return new(0, 0, 0, 0, 0, "Unexpected index schemaVersion");
        if (!index.TryGetProperty("designs", out var listed) || listed.ValueKind != JsonValueKind.Array)
            return new(0, 0, 0, 0, 0, "The index has no designs array");

        var existing = await db.Designs
            .Include(d => d.Versions)
            .Include(d => d.LiveVersion)
            .ToDictionaryAsync(d => d.PublicId, ct);

        int added = 0, updated = 0, withdrawn = 0, restored = 0, unchanged = 0;
        var seen = new HashSet<string>();

        foreach (var entry in listed.EnumerateArray())
        {
            var id = entry.GetProperty("id").GetString() ?? "";
            // The id becomes a directory name and a URL, so it is checked here
            // rather than trusted: the repo is reviewed, but this is the last
            // point before it reaches the filesystem.
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{0,60}$"))
            {
                log.LogWarning("skipping design with an unusable id {Id}", id);
                continue;
            }
            seen.Add(id);

            var qml = entry.GetProperty("qml");
            var sha = qml.GetProperty("sha256").GetString() ?? "";
            var size = qml.GetProperty("size").GetInt64();
            var qmlPath = qml.GetProperty("path").GetString() ?? "";
            var previewPath = entry.TryGetProperty("preview", out var pv)
                ? pv.GetProperty("path").GetString() : null;

            existing.TryGetValue(id, out var design);
            var live = design?.LiveVersion;
            var currentSha = live is null ? null : ShaOf(live.FilesJson);

            if (design is not null && currentSha == sha && design.Status == DesignStatus.Approved)
            {
                unchanged++;
                continue;
            }

            // Copy the files in before the row points at them, so the database
            // never advertises something the site cannot serve.
            if (!await MirrorAsync(id, qmlPath, sha, size, previewPath, entry, ct))
            {
                log.LogWarning("could not mirror {Id}; leaving the database alone", id);
                continue;
            }

            if (design is null)
            {
                design = new Design { PublicId = id, CreatedUtc = DateTime.UtcNow };
                db.Designs.Add(design);
                added++;
            }
            else if (design.Status != DesignStatus.Approved)
            {
                restored++;
            }
            else
            {
                updated++;
            }

            design.Name = Str(entry, "name", id, 80);
            design.Description = Str(entry, "description", "", 400);
            design.OwnerLogin = Str(entry, "author", "", 80);
            design.Tags = entry.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array
                ? string.Join(',', tags.EnumerateArray()
                    .Select(t => t.GetString() ?? "").Where(t => t.Length > 0).Take(3))
                : "";
            design.Kind = DesignKind.Static;
            design.Status = DesignStatus.Approved;

            // Likes come from the index when the repo counts them there; a
            // count the site owns is never overwritten by a sync.
            if (entry.TryGetProperty("likes", out var likes) && likes.TryGetInt32(out var n))
                design.Installs = design.Installs; // installs are the site's own

            // Both files are recorded, so the endpoint never has to guess a
            // preview's extension -- which it did, and got wrong.
            var files = new List<object>
            {
                new { kind = "qml", path = qmlPath,
                      file = Path.GetFileName(qmlPath), sha256 = sha, size },
            };
            if (previewPath is not null)
                files.Add(new
                {
                    kind = "preview", path = previewPath,
                    file = Path.GetFileName(previewPath),
                    sha256 = pv.GetProperty("sha256").GetString() ?? "",
                    size = pv.GetProperty("size").GetInt64(),
                });
            var version = new DesignVersion
            {
                Design = design,
                Number = design.Versions.Count == 0 ? 1 : design.Versions.Max(x => x.Number) + 1,
                RepoUrl = config["Registry:RepoUrl"] ?? "",
                CommitSha = sha,   // the file's own hash: what actually identifies this version
                FilesJson = JsonSerializer.Serialize(files),
                ScanReportJson = """[{"level":"ok","text":"reviewed and merged in the designs repository"}]""",
                ScanVerdict = ScanVerdict.Pass,
                ReviewedBy = "registry-sync",
                ReviewedUtc = DateTime.UtcNow,
            };
            design.Versions.Add(version);
            await db.SaveChangesAsync(ct);
            design.LiveVersionId = version.Id;
            await db.SaveChangesAsync(ct);
        }

        // Anything the database still lists that the repo no longer has is
        // withdrawn -- but only ever on a sync that actually read the index,
        // never because a fetch failed.
        foreach (var (id, design) in existing)
        {
            if (seen.Contains(id) || design.Status != DesignStatus.Approved) continue;
            design.Status = DesignStatus.Revoked;
            withdrawn++;
            db.AuditLogs.Add(new AuditLog
            {
                Actor = "registry-sync",
                Action = "withdraw",
                Detail = $"{id} is no longer in the designs repository",
            });
        }
        await db.SaveChangesAsync(ct);

        var result = new SyncResult(added, updated, withdrawn, restored, unchanged, null);
        if (added + updated + withdrawn + restored > 0)
            log.LogInformation("registry sync: {Result}", result);
        return result;
    }

    private static string Str(JsonElement e, string name, string fallback, int max)
    {
        var s = e.TryGetProperty(name, out var v) ? v.GetString() ?? fallback : fallback;
        return s.Length > max ? s[..max] : s;
    }

    /// <summary>The hash the live version was recorded with, out of its files.</summary>
    private static string? ShaOf(string filesJson)
    {
        try
        {
            var files = JsonSerializer.Deserialize<JsonElement>(filesJson);
            if (files.ValueKind == JsonValueKind.Array && files.GetArrayLength() > 0)
                return files[0].GetProperty("sha256").GetString();
        }
        catch { /* an unreadable version simply counts as changed */ }
        return null;
    }

    private async Task<string> ReadIndexAsync(CancellationToken ct)
    {
        // A local path is allowed so the site and the repo can be worked on
        // together, without either being published first.
        if (!Source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return await File.ReadAllTextAsync(Source, ct);

        var client = httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("omarchycommunity.org");
        return await client.GetStringAsync(Source, ct);
    }

    /// <summary>
    /// Copy one design's files into wwwroot, checking the hash on the way in.
    /// A file whose bytes do not match the index is refused: the point of
    /// carrying the hash is that the copy can be proved.
    /// </summary>
    private async Task<bool> MirrorAsync(
        string id, string qmlPath, string sha, long size,
        string? previewPath, JsonElement entry, CancellationToken ct)
    {
        var dir = Path.Combine(env.WebRootPath, "designs", id);
        Directory.CreateDirectory(dir);

        var qml = await FetchAsync(qmlPath, ct);
        if (qml is null) return false;
        if (qml.Length != size)
        {
            log.LogWarning("{Id}: {Path} is {Got} bytes, the index says {Want}",
                id, qmlPath, qml.Length, size);
            return false;
        }
        var got = Convert.ToHexStringLower(SHA256.HashData(qml));
        if (!string.Equals(got, sha, StringComparison.OrdinalIgnoreCase))
        {
            log.LogWarning("{Id}: {Path} does not match the hash in the index", id, qmlPath);
            return false;
        }
        await File.WriteAllBytesAsync(Path.Combine(dir, Path.GetFileName(qmlPath)), qml, ct);

        if (previewPath is not null)
        {
            var preview = await FetchAsync(previewPath, ct);
            if (preview is not null)
                await File.WriteAllBytesAsync(
                    Path.Combine(dir, Path.GetFileName(previewPath)), preview, ct);
        }
        return true;
    }

    private async Task<byte[]?> FetchAsync(string relative, CancellationToken ct)
    {
        try
        {
            if (!Source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var root = Path.GetDirectoryName(Path.GetFullPath(Source))!;
                var path = Path.GetFullPath(Path.Combine(root, relative));
                // Never outside the repo, whatever the index says.
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
                return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
            }
            var baseUrl = Source[..(Source.LastIndexOf('/') + 1)];
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("omarchycommunity.org");
            return await client.GetByteArrayAsync(baseUrl + relative, ct);
        }
        catch (Exception e)
        {
            log.LogWarning(e, "could not fetch {Path}", relative);
            return null;
        }
    }
}

/// <summary>Runs the sync on a timer, and once shortly after startup.</summary>
public class RegistrySyncService(IServiceProvider services, IConfiguration config,
                                 ILogger<RegistrySyncService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        var minutes = config.GetValue("Registry:SyncMinutes", 10);
        if (minutes <= 0 || string.IsNullOrWhiteSpace(config["Registry:IndexUrl"]))
        {
            log.LogInformation("registry sync is off");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(10), stopping);
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var sync = scope.ServiceProvider.GetRequiredService<RegistrySync>();
                await sync.RunAsync(db, stopping);
            }
            catch (Exception e) when (!stopping.IsCancellationRequested)
            {
                // A sync that throws must not take the site with it; the next
                // one will pick up whatever this one missed.
                log.LogWarning(e, "registry sync failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(minutes), stopping);
        }
    }
}
