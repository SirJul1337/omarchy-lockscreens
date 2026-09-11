using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OmarchyLockscreens.Pages.Admin;

public class LoginModel : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Error { get; set; }

    public IActionResult OnGet()
    {
        if (User.IsInRole("admin")) return Redirect("/");
        return Page();
    }
}
