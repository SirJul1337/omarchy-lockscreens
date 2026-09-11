using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OmarchyLockscreens.Data;
using OmarchyLockscreens.Models;

namespace OmarchyLockscreens.Pages.Designs;

public class DetailModel(AppDbContext db, IWebHostEnvironment env) : PageModel
{
    public Design Design { get; private set; } = null!;

    /// <summary>The QML itself, so it can be copied straight off the page.</summary>
    /// <remarks>
    /// The plugin's one-press install is not live yet, and until it is this is
    /// the only way to actually use a design. These are the mirrored bytes --
    /// the same ones the registry advertises and the plugin would download.
    ///
    /// Saved into ~/.config/omarchy/lock-designs/ by hand it needs no editing:
    /// the import in the template is written for exactly that depth. Only the
    /// plugin's own install puts a design a level deeper and has to rewrite it.
    /// </remarks>
    public string? QmlText { get; private set; }
    public string? QmlFileName { get; private set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var design = await db.Designs
            .AsNoTracking()
            .Include(d => d.LiveVersion)
            .FirstOrDefaultAsync(d => d.PublicId == id);
        if (design is null || design.Status is not (DesignStatus.Approved or DesignStatus.Unlisted))
            return NotFound();
        Design = design;

        var dir = Path.Combine(env.WebRootPath, "designs", design.PublicId);
        if (Directory.Exists(dir))
        {
            var qml = Directory.EnumerateFiles(dir, "*.qml").FirstOrDefault();
            if (qml is not null)
            {
                QmlFileName = Path.GetFileName(qml);
                QmlText = await System.IO.File.ReadAllTextAsync(qml);
            }
        }

        return Page();
    }
}
