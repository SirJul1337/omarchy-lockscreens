using System.ComponentModel.DataAnnotations;

namespace OmarchyLockscreens.Models;

public enum DesignStatus { Pending, Approved, Rejected, Revoked, Unlisted }
public enum DesignKind { Static, Video }
public enum ScanVerdict { None, Pass, Warn, Fail }

// No visitor accounts: submissions arrive as pull requests and the maintainer
// curates them in (Omarchy-plugins style). "Owner" is just the submitter's
// GitHub handle, stored as text — there is no user table and no user login.
public class Design
{
    public int Id { get; set; }
    [MaxLength(160)] public string PublicId { get; set; } = "";
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(400)] public string Description { get; set; } = "";
    [MaxLength(80)] public string Tags { get; set; } = "";      // comma separated, lowercase
    [MaxLength(80)] public string OwnerLogin { get; set; } = ""; // submitter's GitHub handle
    public DesignKind Kind { get; set; }
    public DesignStatus Status { get; set; } = DesignStatus.Pending;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int? LiveVersionId { get; set; }
    public DesignVersion? LiveVersion { get; set; }
    public int Installs { get; set; }
    /// <summary>Kept here rather than in the repo: it changes without anyone
    /// opening a pull request, which is the line between the two.</summary>
    public int Likes { get; set; }
    public List<DesignVersion> Versions { get; set; } = [];

    public string[] TagList => Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class DesignVersion
{
    public int Id { get; set; }
    public int DesignId { get; set; }
    public Design Design { get; set; } = null!;
    public int Number { get; set; }
    [MaxLength(300)] public string RepoUrl { get; set; } = "";
    [MaxLength(40)] public string CommitSha { get; set; } = "";
    public string FilesJson { get; set; } = "[]";
    public string ScanReportJson { get; set; } = "[]";
    public ScanVerdict ScanVerdict { get; set; } = ScanVerdict.None;
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(80)] public string? ReviewedBy { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    [MaxLength(400)] public string? ReviewNote { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    [MaxLength(80)] public string Actor { get; set; } = "";
    [MaxLength(60)] public string Action { get; set; } = "";
    [MaxLength(400)] public string Detail { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
