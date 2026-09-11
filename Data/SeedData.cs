using Microsoft.EntityFrameworkCore;
using OmarchyLockscreens.Models;

namespace OmarchyLockscreens.Data;

/// <summary>
/// Seeds the directory with the real built-in designs from the Lock Screen
/// Explorer (by SirJul1337), pointing at the actual repo. Previews are rendered
/// from _Preview.cshtml, keyed by the slug here.
/// </summary>
public static class SeedData
{
    private const string Repo = "https://github.com/SirJul1337/omarchy-lock-explorer";
    private const string Author = "SirJul1337";

    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        if (await db.Designs.AnyAsync()) return;

        Design Approved(string slug, string file, string name, string desc, string tags, DesignKind kind, int installs)
        {
            var d = new Design
            {
                PublicId = $"com.github.sirjul1337.{slug}",
                Name = name, Description = desc, Tags = tags, Kind = kind,
                Status = DesignStatus.Approved, OwnerLogin = Author, Installs = installs,
                CreatedUtc = DateTime.UtcNow.AddDays(-Random.Shared.Next(3, 40)),
            };
            var sha = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(20));
            d.Versions.Add(new DesignVersion
            {
                Design = d, Number = 1,
                RepoUrl = Repo,
                CommitSha = sha,
                FilesJson = $$"""[{"path":"designs/{{file}}","url":"https://raw.githubusercontent.com/SirJul1337/omarchy-lock-explorer/main/designs/{{file}}","sha256":"{{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))}}","size":5400}]""",
                ScanReportJson = """[{"level":"ok","text":"imports are allowlisted"},{"level":"ok","text":"no processes, network, eval or file access"}]""",
                ScanVerdict = ScanVerdict.Pass,
                ReviewedBy = Author, ReviewedUtc = DateTime.UtcNow.AddDays(-2),
            });
            return d;
        }

        db.Designs.AddRange(
            Approved("card", "Card.qml", "Greeting Card", "Clock, avatar and greeting on a frosted card.", "cards,clock", DesignKind.Static, 640),
            Approved("classic", "Classic.qml", "Classic", "The stock Omarchy lock screen — clean and centered.", "minimal", DesignKind.Static, 512),
            Approved("editorial", "Editorial.qml", "Editorial", "Big clock bottom left, sign-in field underneath.", "typography", DesignKind.Static, 388),
            Approved("zen", "Zen.qml", "Zen", "No input box — just start typing.", "minimal", DesignKind.Static, 356),
            Approved("split", "Split.qml", "Split", "Wallpaper on the left, sign-in panel on the right.", "cards", DesignKind.Static, 331),
            Approved("terminal", "Terminal.qml", "Terminal", "TTY-style login prompt with a blinking cursor.", "dark,fun", DesignKind.Static, 402),
            Approved("ring", "Ring.qml", "Ring", "A progress ring around the clock fills as you type.", "clock,minimal", DesignKind.Static, 274),
            Approved("poster", "Poster.qml", "Poster", "Huge stacked hours and minutes.", "typography", DesignKind.Static, 251),
            Approved("dock", "Dock.qml", "Dock", "Everything tucked into a slim bar at the bottom.", "minimal", DesignKind.Static, 233),
            Approved("neon", "Neon.qml", "Neon", "A glowing clock on a dark grid.", "dark,fun", DesignKind.Static, 219),
            Approved("rain", "Rain.qml", "Rain", "Falling glyphs — you know the movie.", "dark,fun", DesignKind.Static, 305),
            Approved("analog", "Analog.qml", "Analog", "A clean analog clock face.", "clock", DesignKind.Static, 188),
            Approved("flip", "Flip.qml", "Flip", "Flip-clock tiles that turn on the minute.", "clock,fun", DesignKind.Static, 197)
        );

        // Two saves: the circular Design -> LiveVersion FK is set once rows exist.
        await db.SaveChangesAsync();
        foreach (var d in db.Designs.Local.Where(d => d.Status == DesignStatus.Approved))
            d.LiveVersion = d.Versions.OrderByDescending(v => v.Number).First();
        await db.SaveChangesAsync();
    }
}
