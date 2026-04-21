using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
            "Admin" => RedirectToPage("/Admin/Index"),
            "Tecnico" => RedirectToPage("/Tecnico/Index"),
            _ => RedirectToPage("/Usuario/Index")
        };
    }
}