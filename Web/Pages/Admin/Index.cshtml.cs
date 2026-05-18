using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;
using Shared;

namespace Web.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public int TotalIncidencias { get; set; }
    public int Abiertas { get; set; }
    public int EnProceso { get; set; }
    public int Cerradas { get; set; }
    public Dictionary<string, int> PorPrioridad { get; set; } = new();
    public string TiempoMedioResolucion { get; set; } = "Sin datos";
    public string TecnicoMasCarga { get; set; } = "Sin datos";

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var rol = HttpContext.Session.GetString("Rol");
        if (rol != Roles.Admin)
            return RedirectToPage("/Index");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Incidencias/stats");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var stats = JsonSerializer.Deserialize<StatsDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (stats != null)
            {
                TotalIncidencias = stats.Total;
                Abiertas         = stats.Abiertas;
                EnProceso        = stats.EnProceso;
                Cerradas         = stats.Cerradas;
                PorPrioridad     = stats.PorPrioridad;

                if (stats.TiempoMedioHoras.HasValue)
                    TiempoMedioResolucion = $"{stats.TiempoMedioHoras.Value} horas";

                if (stats.TecnicoMasCargaNombre != null && stats.TecnicoMasCargaCount.HasValue)
                    TecnicoMasCarga = $"{stats.TecnicoMasCargaNombre} ({stats.TecnicoMasCargaCount.Value} incidencias)";
            }
        }

        return Page();
    }

    private class StatsDto
    {
        public int Total { get; set; }
        public int Abiertas { get; set; }
        public int EnProceso { get; set; }
        public int Cerradas { get; set; }
        public Dictionary<string, int> PorPrioridad { get; set; } = new();
        public double? TiempoMedioHoras { get; set; }
        public string? TecnicoMasCargaNombre { get; set; }
        public int? TecnicoMasCargaCount { get; set; }
    }
}