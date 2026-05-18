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
    public int PaginaActual { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int Total { get; set; } = 0;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> OnGetAsync(int pagina = 1)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        NombreUsuario = HttpContext.Session.GetString("Nombre") ?? "";
        Rol = HttpContext.Session.GetString("Rol") ?? "";
        PaginaActual = pagina;

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/Incidencias?pagina={pagina}&tamanio=10");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var resultado = JsonSerializer.Deserialize<PaginadoModel<IncidenciaModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Incidencias = resultado?.Datos ?? new();
            TotalPaginas = resultado?.TotalPaginas ?? 1;
            Total = resultado?.Total ?? 0;
        }

        return Page();
    }
}