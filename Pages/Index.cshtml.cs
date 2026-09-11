using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OmarchyLockscreens.Data;
using OmarchyLockscreens.Models;

namespace OmarchyLockscreens.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Design> Featured { get; private set; } = [];
    public int DesignCount { get; private set; }

    public async Task OnGetAsync()
    {
        DesignCount = await db.Designs.CountAsync(d => d.Status == DesignStatus.Approved);
        Featured = await db.Designs
            .AsNoTracking()
            .Include(d => d.LiveVersion)
            .Where(d => d.Status == DesignStatus.Approved)
            .OrderByDescending(d => d.CreatedUtc)
            .Take(3)
            .ToListAsync();
    }
}
