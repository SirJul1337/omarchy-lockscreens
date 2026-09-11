using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OmarchyLockscreens.Pages;

public class ApplyModel(IConfiguration config) : PageModel
{
    public string IssueUrl { get; private set; } = "";

    public void OnGet()
    {
        var baseUrl = config["Apply:IssueBaseUrl"] ?? "https://github.com/SirJul1337/omarchy-lock-explorer/issues/new";
        var title = Uri.EscapeDataString("[design] <your design name>");
        var body = Uri.EscapeDataString(
            """
            <!-- Fill this in. A maintainer reviews it and adds it to the directory. -->

            **Name:**
            **Your GitHub:** @
            **Repository:** https://github.com/<you>/<repo>
            **Commit or tag to pin:**
            **Design file (.qml):**
            **Video file (optional, .mp4/.webm):**
            **Tags (up to 3):** minimal, clock, dark, cards, motion, reactive, typography, fun
            **Description (one line):**

            <!-- Your files stay in your repo; we pin the commit above and verify SHA-256 on install. -->
            """);
        IssueUrl = $"{baseUrl}?labels=design-submission&title={title}&body={body}";
    }
}
