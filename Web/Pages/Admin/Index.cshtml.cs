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
    public int Resueltas { get; set; }
    public int Cerradas { get; set; }
    public Dictionary<string, int> PorPrioridad { get; set; } = new();
    public string TiempoMedioResolucion { get; set; } = "Sin datos";
    public string TecnicoMasCarga { get; set; } = "Sin datos";
    public Dictionary<string, double?> TiempoMedioPorPrioridad { get; set; } = new();

    // JSON pre-serializado para Chart.js
    public string PorDiaLabelsJson { get; set; } = "[]";
    public string PorDiaDataJson { get; set; } = "[]";
    public string PorPrioridadLabelsJson { get; set; } = "[]";
    public string PorPrioridadDataJson { get; set; } = "[]";
    public string PorCategoriaLabelsJson { get; set; } = "[]";
    public string PorCategoriaDataJson { get; set; } = "[]";
    public string PorTecnicoLabelsJson { get; set; } = "[]";
    public string PorTecnicoDataJson { get; set; } = "[]";
    public string PorUsuarioLabelsJson { get; set; } = "[]";
    public string PorUsuarioDataJson { get; set; } = "[]";

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
                Resueltas        = stats.Resueltas;
                Cerradas         = stats.Cerradas;
                PorPrioridad     = stats.PorPrioridad;

                if (stats.TiempoMedioHoras.HasValue)
                    TiempoMedioResolucion = $"{stats.TiempoMedioHoras.Value} horas";

                if (stats.TecnicoMasCargaNombre != null && stats.TecnicoMasCargaCount.HasValue)
                    TecnicoMasCarga = $"{stats.TecnicoMasCargaNombre} ({stats.TecnicoMasCargaCount.Value} incidencias)";

                TiempoMedioPorPrioridad = stats.TiempoMedioPorPrioridad;

                var opts = new JsonSerializerOptions();
                PorPrioridadLabelsJson = JsonSerializer.Serialize(stats.PorPrioridad.Keys.ToArray(), opts);
                PorPrioridadDataJson   = JsonSerializer.Serialize(stats.PorPrioridad.Values.ToArray(), opts);
                PorDiaLabelsJson       = JsonSerializer.Serialize(stats.PorDia.Select(x => x.Etiqueta).ToArray(), opts);
                PorDiaDataJson         = JsonSerializer.Serialize(stats.PorDia.Select(x => x.Total).ToArray(), opts);
                PorCategoriaLabelsJson = JsonSerializer.Serialize(stats.PorCategoria.Select(x => x.Categoria).ToArray(), opts);
                PorCategoriaDataJson   = JsonSerializer.Serialize(stats.PorCategoria.Select(x => x.Total).ToArray(), opts);
                PorTecnicoLabelsJson   = JsonSerializer.Serialize(stats.PorTecnico.Select(x => x.Nombre).ToArray(), opts);
                PorTecnicoDataJson     = JsonSerializer.Serialize(stats.PorTecnico.Select(x => x.Total).ToArray(), opts);
                PorUsuarioLabelsJson   = JsonSerializer.Serialize(stats.PorUsuario.Select(x => x.Nombre).ToArray(), opts);
                PorUsuarioDataJson     = JsonSerializer.Serialize(stats.PorUsuario.Select(x => x.Total).ToArray(), opts);
            }
        }

        return Page();
    }

    private class StatsDto
    {
        public int Total { get; set; }
        public int Abiertas { get; set; }
        public int EnProceso { get; set; }
        public int Resueltas { get; set; }
        public int Cerradas { get; set; }
        public Dictionary<string, int> PorPrioridad { get; set; } = new();
        public double? TiempoMedioHoras { get; set; }
        public Dictionary<string, double?> TiempoMedioPorPrioridad { get; set; } = new();
        public string? TecnicoMasCargaNombre { get; set; }
        public int? TecnicoMasCargaCount { get; set; }
        public List<DiaDato> PorDia { get; set; } = new();
        public List<CategoriaDato> PorCategoria { get; set; } = new();
        public List<NombreTotalDato> PorTecnico { get; set; } = new();
        public List<NombreTotalDato> PorUsuario { get; set; } = new();
    }

    private class DiaDato
    {
        public string Etiqueta { get; set; } = "";
        public int Total { get; set; }
    }

    private class CategoriaDato
    {
        public string Categoria { get; set; } = "";
        public int Total { get; set; }
    }

    private class NombreTotalDato
    {
        public string Nombre { get; set; } = "";
        public int Total { get; set; }
    }
}
