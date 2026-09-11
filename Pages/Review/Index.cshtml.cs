using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OmarchyLockscreens.Data;
using OmarchyLockscreens.Models;
using OmarchyLockscreens.Services;

namespace OmarchyLockscreens.Pages.Review;

public class IndexModel(AppDbContext db, GitHubContentService github) : PageModel
{
    public List<Design> Queue { get; private set; } = [];
    public List<Design> Live { get; private set; } = [];
    public string? Error { get; private set; }
    public string? Notice { get; private set; }

    // Add-design form
    [BindProperty] public string RepoUrl { get; set; } = "";
    [BindProperty] public string Reference { get; set; } = "";
    [BindProperty] public string QmlPath { get; set; } = "";
    [BindProperty] public string? VideoPath { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string Description { get; set; } = "";
    [BindProperty] public string TagInput { get; set; } = "";
    [BindProperty] public string OwnerLogin { get; set; } = "";
    [BindProperty] public string? IssueUrl { get; set; }

    private static readonly string[] ValidTags =
        ["minimal", "cards", "clock", "typography", "dark", "fun", "motion", "reactive"];

    public async Task OnGetAsync()
    {
        Error = TempData["Error"] as string;
        Notice = TempData["Notice"] as string;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Queue = await db.Designs.AsNoTracking()
            .Where(d => d.Status == DesignStatus.Pending)
            .Include(d => d.Versions)
            .OrderBy(d => d.CreatedUtc)
            .ToListAsync();
        Live = await db.Designs.AsNoTracking()
            .Where(d => d.Status == DesignStatus.Approved || d.Status == DesignStatus.Unlisted)
            .Include(d => d.LiveVersion)
            .OrderByDescending(d => d.CreatedUtc)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken ct)
    {
        if (!GitHubContentService.TryParseRepo(RepoUrl, out var owner, out var repo))
            return Fail("Not a valid github.com repository URL.");
        OwnerLogin = string.IsNullOrWhiteSpace(OwnerLogin) ? owner : OwnerLogin.Trim().TrimStart('@');
        Name = Name.Trim(); Description = Description.Trim();
        if (Name.Length is < 2 or > 40) return Fail("Name must be 2–40 characters.");
        if (Description.Length is < 10 or > 400) return Fail("Description must be 10–400 characters.");

        var tags = TagInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant()).Where(ValidTags.Contains).Distinct().Take(3).ToArray();
        if (tags.Length == 0) return Fail("Pick at least one valid tag.");

        QmlPath = QmlPath.Trim().TrimStart('/');
        if (!QmlPath.EndsWith(".qml") || QmlPath.Contains("..")) return Fail("Design file must be a .qml path in the repo.");
        VideoPath = string.IsNullOrWhiteSpace(VideoPath) ? null : VideoPath.Trim().TrimStart('/');
        if (VideoPath is not null && (VideoPath.Contains("..") || !(VideoPath.EndsWith(".mp4") || VideoPath.EndsWith(".webm"))))
            return Fail("Video must be an .mp4 or .webm path in the repo.");

        var sha = await github.ResolveCommitAsync(owner, repo, Reference.Trim(), ct);
        if (sha is null) return Fail("Could not resolve that tag/commit — is the repo public?");

        var qml = await github.FetchPinnedAsync(owner, repo, sha, QmlPath, GitHubContentService.MaxQmlBytes, false, ct);
        if (qml is null) return Fail($"Could not fetch {QmlPath} at {sha[..7]} (missing or over 64 KB).");
        var (lines, verdict) = ScanService.ScanQml(QmlPath, qml.Value.Content!);
        var files = new List<PinnedFile> { qml.Value.File };
        if (VideoPath is not null)
        {
            var video = await github.FetchPinnedAsync(owner, repo, sha, VideoPath, GitHubContentService.MaxVideoBytes, true, ct);
            if (video is null) return Fail($"Could not fetch {VideoPath} at {sha[..7]} (missing or over 50 MB).");
            files.Add(video.Value.File);
            lines.Add(ScanService.VideoSizeLine(VideoPath, video.Value.File.Size));
            if (verdict == ScanVerdict.Pass && lines.Any(l => l.Level == "warn")) verdict = ScanVerdict.Warn;
        }

        var slug = new string(Name.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        if (slug.Length == 0) return Fail("Name needs at least one letter or digit.");
        var publicId = $"com.github.{OwnerLogin.ToLowerInvariant()}.{slug}";

        var design = await db.Designs.Include(d => d.Versions).FirstOrDefaultAsync(d => d.PublicId == publicId, ct);
        if (design is null)
        {
            design = new Design { PublicId = publicId };
            db.Designs.Add(design);
        }
        design.Name = Name;
        design.Description = Description;
        design.Tags = string.Join(',', tags);
        design.OwnerLogin = OwnerLogin;
        design.Kind = VideoPath is null ? DesignKind.Static : DesignKind.Video;
        design.IssueUrl = string.IsNullOrWhiteSpace(IssueUrl) ? null : IssueUrl.Trim();
        if (design.Status is not DesignStatus.Approved) design.Status = DesignStatus.Pending;

        design.Versions.Add(new DesignVersion
        {
            Design = design,
            Number = design.Versions.Count == 0 ? 1 : design.Versions.Max(x => x.Number) + 1,
            RepoUrl = $"https://github.com/{owner}/{repo}",
            CommitSha = sha,
            FilesJson = JsonSerializer.Serialize(files.Select(f => new { path = f.Path, url = f.Url, sha256 = f.Sha256, size = f.Size })),
            ScanReportJson = JsonSerializer.Serialize(lines.Select(l => new { level = l.Level, text = l.Text })),
            ScanVerdict = verdict,
        });
        db.AuditLogs.Add(new AuditLog { Actor = "admin", Action = "add", Detail = $"{publicId} @ {sha[..7]} scan={verdict}" });
        await db.SaveChangesAsync(ct);
        TempData["Notice"] = $"Added {Name} — scan {verdict}. Review and approve below.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApproveAsync(int versionId)
    {
        var v = await db.DesignVersions.Include(x => x.Design).FirstOrDefaultAsync(x => x.Id == versionId);
        if (v is null) return RedirectToPage();
        v.ReviewedBy = "admin"; v.ReviewedUtc = DateTime.UtcNow;
        v.Design.LiveVersionId = v.Id;
        v.Design.Status = DesignStatus.Approved;
        db.AuditLogs.Add(new AuditLog { Actor = "admin", Action = "approve", Detail = $"{v.Design.PublicId} v{v.Number}" });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int versionId, string? note)
    {
        var v = await db.DesignVersions.Include(x => x.Design).FirstOrDefaultAsync(x => x.Id == versionId);
        if (v is null) return RedirectToPage();
        v.ReviewedBy = "admin"; v.ReviewedUtc = DateTime.UtcNow;
        v.ReviewNote = string.IsNullOrWhiteSpace(note) ? "rejected" : note.Trim();
        if (v.Design.LiveVersionId is null) v.Design.Status = DesignStatus.Rejected;
        db.AuditLogs.Add(new AuditLog { Actor = "admin", Action = "reject", Detail = $"{v.Design.PublicId} v{v.Number}: {v.ReviewNote}" });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync(int designId)
    {
        var d = await db.Designs.FirstOrDefaultAsync(x => x.Id == designId);
        if (d is null) return RedirectToPage();
        d.Status = DesignStatus.Revoked;
        db.AuditLogs.Add(new AuditLog { Actor = "admin", Action = "revoke", Detail = d.PublicId });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    private IActionResult Fail(string message)
    {
        TempData["Error"] = message;
        return RedirectToPage();
    }
}
