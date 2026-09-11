using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OmarchyLockscreens.Data;
using OmarchyLockscreens.Models;

namespace OmarchyLockscreens.Pages.Designs;

public class IndexModel(AppDbContext db) : PageModel
{
    public static readonly string[] Categories =
        ["all", "minimal", "cards", "clock", "typography", "dark", "fun", "motion", "reactive"];

    public List<Design> Designs { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? Tag { get; set; }
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "recent";
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public async Task OnGetAsync()
    {
        var query = db.Designs
            .AsNoTracking()
            .Where(d => d.Status == DesignStatus.Approved)
            .AsQueryable();

        if (!string.IsNullOrEmpty(Tag) && Tag != "all")
            query = query.Where(d => ("," + d.Tags + ",").Contains("," + Tag + ","));
        if (!string.IsNullOrWhiteSpace(Q))
            query = query.Where(d => d.Name.Contains(Q) || d.Description.Contains(Q) || d.OwnerLogin.Contains(Q));

        Designs = Sort == "az"
            ? await query.OrderBy(d => d.Name).ToListAsync()
            : await query.OrderByDescending(d => d.CreatedUtc).ToListAsync();
    }
}
