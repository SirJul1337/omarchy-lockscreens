using System.Security.Cryptography;
using System.Text.Json;

namespace OmarchyLockscreens.Services;

public record PinnedFile(string Path, string Url, string Sha256, long Size);

/// <summary>
/// Anonymous read-only access to public GitHub repos: resolve a ref to a full
/// commit SHA and fetch files pinned to that SHA. An optional token in config
/// ("GitHub:ApiToken") raises the rate limit; nothing here writes anywhere.
/// </summary>
public class GitHubContentService(IHttpClientFactory httpFactory, IConfiguration config)
{
    public const long MaxQmlBytes = 64 * 1024;
    public const long MaxVideoBytes = 50 * 1024 * 1024;

    private HttpClient Api()
    {
        var c = httpFactory.CreateClient("github");
        c.BaseAddress = new Uri("https://api.github.com/");
        c.DefaultRequestHeaders.UserAgent.ParseAdd("omarchycommunity.org");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var token = config["GitHub:ApiToken"];
        if (!string.IsNullOrEmpty(token))
            c.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return c;
    }

    /// <summary>Parses "https://github.com/owner/repo" (with optional .git / trailing slash).</summary>
    public static bool TryParseRepo(string url, out string owner, out string repo)
    {
        owner = repo = "";
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)) return false;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length < 2) return false;
        owner = parts[0];
        repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        return owner.Length > 0 && repo.Length > 0;
    }

    /// <summary>Resolves a branch, tag or (short) SHA to the full commit SHA. Null if not found.</summary>
    public async Task<string?> ResolveCommitAsync(string owner, string repo, string reference, CancellationToken ct)
    {
        using var resp = await Api().GetAsync($"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/commits/{Uri.EscapeDataString(reference)}", ct);
        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("sha", out var sha) ? sha.GetString() : null;
    }

    public static string RawUrl(string owner, string repo, string sha, string path)
        => $"https://raw.githubusercontent.com/{owner}/{repo}/{sha}/{path}";

    /// <summary>Downloads a pinned file up to maxBytes, returning content (unless hashOnly) + SHA-256 + size. Null if missing or over cap.</summary>
    public async Task<(PinnedFile File, byte[]? Content)?> FetchPinnedAsync(
        string owner, string repo, string sha, string path, long maxBytes, bool hashOnly, CancellationToken ct)
    {
        var url = RawUrl(owner, repo, sha, path);
        var c = httpFactory.CreateClient("github");
        c.DefaultRequestHeaders.UserAgent.ParseAdd("omarchycommunity.org");
        c.Timeout = TimeSpan.FromMinutes(4);
        using var resp = await c.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) return null;
        if (resp.Content.Headers.ContentLength is > 0 and var len && len > maxBytes) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var sha256 = SHA256.Create();
        var buffer = new byte[81920];
        long total = 0;
        using var kept = hashOnly ? null : new MemoryStream();
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes) return null;
            sha256.TransformBlock(buffer, 0, read, null, 0);
            kept?.Write(buffer, 0, read);
        }
        sha256.TransformFinalBlock([], 0, 0);
        var hash = Convert.ToHexStringLower(sha256.Hash!);
        return (new PinnedFile(path, url, hash, total), kept?.ToArray());
    }
}
