using System.Text;
using System.Text.RegularExpressions;
using OmarchyLockscreens.Models;

namespace OmarchyLockscreens.Services;

public record ScanLine(string Level, string Text); // ok | warn | fail

/// <summary>
/// The automated gate from the plan: a capability allowlist for QML plus size
/// caps. It decides "is this technically dangerous", never "is this good" —
/// that stays with the human reviewer. The same rules the plugin re-checks
/// on install.
/// </summary>
public static partial class ScanService
{
    private static readonly string[] AllowedImportPrefixes =
    [
        "QtQuick", "QtMultimedia", "qs.Commons"
    ];

    // Tokens that mean capability beyond drawing a lock screen.
    private static readonly (Regex Pattern, string Name)[] Forbidden =
    [
        (ForbiddenProcess(), "Process"),
        (ForbiddenXhr(), "XMLHttpRequest"),
        (ForbiddenSocket(), "Socket/WebSocket"),
        (ForbiddenEval(), "eval"),
        (ForbiddenOpenUrl(), "Qt.openUrlExternally"),
        (ForbiddenCreateQml(), "Qt.createQmlObject"),
        (ForbiddenInclude(), "Qt.include"),
        (ForbiddenXmlListModel(), "XmlListModel"),
        (ForbiddenNetworkUrl(), "network URL"),
        (ForbiddenFileUrl(), "absolute file:// path"),
    ];

    [GeneratedRegex(@"\bProcess\b")] private static partial Regex ForbiddenProcess();
    [GeneratedRegex(@"\bXMLHttpRequest\b")] private static partial Regex ForbiddenXhr();
    [GeneratedRegex(@"\b(WebSocket|SocketServer|Socket)\b")] private static partial Regex ForbiddenSocket();
    [GeneratedRegex(@"\beval\s*\(")] private static partial Regex ForbiddenEval();
    [GeneratedRegex(@"Qt\s*\.\s*openUrlExternally")] private static partial Regex ForbiddenOpenUrl();
    [GeneratedRegex(@"Qt\s*\.\s*createQmlObject")] private static partial Regex ForbiddenCreateQml();
    [GeneratedRegex(@"Qt\s*\.\s*include\s*\(")] private static partial Regex ForbiddenInclude();
    [GeneratedRegex(@"\bXmlListModel\b")] private static partial Regex ForbiddenXmlListModel();
    [GeneratedRegex(@"https?://", RegexOptions.IgnoreCase)] private static partial Regex ForbiddenNetworkUrl();
    [GeneratedRegex(@"file:///", RegexOptions.IgnoreCase)] private static partial Regex ForbiddenFileUrl();
    [GeneratedRegex(@"^\s*import\s+(?:""(?<quoted>[^""]+)""|(?<module>[A-Za-z0-9_.]+))", RegexOptions.Multiline)]
    private static partial Regex ImportLine();

    public static (List<ScanLine> Lines, ScanVerdict Verdict) ScanQml(string fileName, byte[] content)
    {
        var lines = new List<ScanLine>();
        var fail = false;
        var warn = false;

        if (content.Length > GitHubContentService.MaxQmlBytes)
        {
            lines.Add(new("fail", $"{fileName} is {content.Length / 1024} KB — over the 64 KB cap"));
            return (lines, ScanVerdict.Fail);
        }

        var text = Encoding.UTF8.GetString(content);

        foreach (Match m in ImportLine().Matches(text))
        {
            var module = m.Groups["module"].Success ? m.Groups["module"].Value : null;
            var quoted = m.Groups["quoted"].Success ? m.Groups["quoted"].Value : null;
            if (module is not null)
            {
                if (AllowedImportPrefixes.Any(p => module == p || module.StartsWith(p + ".")))
                    continue;
                lines.Add(new("fail", $"{fileName}: import {module} is not on the allowlist"));
                fail = true;
            }
            else if (quoted is not null)
            {
                // Relative import of the plugin's designs dir is the documented pattern.
                if (quoted.Contains("lock-explorer/designs") || quoted == "../designs" || quoted == "designs")
                    continue;
                lines.Add(new("fail", $"{fileName}: directory import \"{quoted}\" is not allowed"));
                fail = true;
            }
        }
        if (!fail) lines.Add(new("ok", $"{fileName}: imports are allowlisted"));

        var hits = new List<string>();
        foreach (var (pattern, name) in Forbidden)
            if (pattern.IsMatch(text)) hits.Add(name);
        if (hits.Count > 0)
        {
            lines.Add(new("fail", $"{fileName}: forbidden capability: {string.Join(", ", hits)}"));
            fail = true;
        }
        else
        {
            lines.Add(new("ok", $"{fileName}: no processes, network, eval or file access"));
        }

        lines.Add(new("ok", $"{fileName}: {content.Length / 1024.0:0.0} KB, hashed at the pinned commit"));

        var verdict = fail ? ScanVerdict.Fail : warn ? ScanVerdict.Warn : ScanVerdict.Pass;
        return (lines, verdict);
    }

    public static ScanLine VideoSizeLine(string fileName, long size)
    {
        var mb = size / (1024.0 * 1024.0);
        return mb > 45
            ? new("warn", $"{fileName} is {mb:0} MB — close to the 50 MB cap")
            : new("ok", $"{fileName}: {mb:0.0} MB, hashed at the pinned commit");
    }
}
