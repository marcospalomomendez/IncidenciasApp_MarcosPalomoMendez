using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Usuario;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public List<IncidenciaModel> Incidencias { get; set; } = new();
    public int PaginaActual { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int Total { get; set; } = 0;
    public string FiltroActual { get; set; } = "Todas";

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> OnGetAsync(int pagina = 1, string filtro = "Todas")
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var rol = HttpContext.Session.GetString("Rol");
        if (rol != "Usuario" && rol != "Admin")
            return RedirectToPage("/Index");

        PaginaActual = pagina;
        FiltroActual = filtro;

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var url = $"/api/Incidencias/mis?pagina={pagina}&tamanio=10";
        if (filtro != "Todas")
            url += $"&estado={filtro}";

        var response = await client.GetAsync(url);
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

    public async Task<IActionResult> OnPostAsync(int id, string nuevoEstado)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var body = new StringContent(
            JsonSerializer.Serialize(new { estado = nuevoEstado }),
            Encoding.UTF8, "application/json");

        await client.PutAsync($"/api/Incidencias/{id}", body);
        return RedirectToPage();
    }
}