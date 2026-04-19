using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Incidencias;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public List<IncidenciaModel> Incidencias { get; set; } = new();
    public string NombreUsuario { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        NombreUsuario = HttpContext.Session.GetString("Nombre") ?? "";
        Rol = HttpContext.Session.GetString("Rol") ?? "";

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Incidencias");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            Incidencias = JsonSerializer.Deserialize<List<IncidenciaModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        return Page();
    }
}