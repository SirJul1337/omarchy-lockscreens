using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OmarchyLockscreens.Pages;

public class ApplyModel(IConfiguration config) : PageModel
{
    /// <summary>The repository a submission is a pull request against.</summary>
    public string RepoUrl { get; private set; } = "";

    /// <summary>The full instructions, in that repository.</summary>
    public string ContributingUrl { get; private set; } = "";

    public void OnGet()
    {
        RepoUrl = (config["Registry:RepoUrl"] ?? "https://github.com/SirJul1337/omarchy-lockscreens")
            .TrimEnd('/');
        ContributingUrl = $"{RepoUrl}/blob/main/CONTRIBUTING.md";
    }
}
