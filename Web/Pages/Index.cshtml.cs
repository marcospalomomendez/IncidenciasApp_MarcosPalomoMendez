using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared;

namespace Web.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        var rol = HttpContext.Session.GetString("Rol");

        if (string.IsNullOrEmpty(rol))
            return RedirectToPage("/Login");

        return rol switch
        {
            Roles.Admin => RedirectToPage("/Admin/Index"),
            Roles.Tecnico => RedirectToPage("/Tecnico/Index"),
            _ => RedirectToPage("/Usuario/Index")
        };
    }
}