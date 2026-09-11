using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OmarchyLockscreens.Data;
using OmarchyLockscreens.Models;

namespace OmarchyLockscreens.Pages.Designs;

public class DetailModel(AppDbContext db) : PageModel
{
    public Design Design { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var design = await db.Designs
            .AsNoTracking()
            .Include(d => d.LiveVersion)
            .FirstOrDefaultAsync(d => d.PublicId == id);
        if (design is null || design.Status is not (DesignStatus.Approved or DesignStatus.Unlisted))
            return NotFound();
        Design = design;
        return Page();
    }
}
