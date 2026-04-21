using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;
using Web.Models;

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
        if (rol != "Admin")
            return RedirectToPage("/Index");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Incidencias");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var incidencias = JsonSerializer.Deserialize<List<IncidenciaModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            TotalIncidencias = incidencias.Count;
            Abiertas = incidencias.Count(i => i.Estado == "Abierta");
            EnProceso = incidencias.Count(i => i.Estado == "EnProceso");
            Cerradas = incidencias.Count(i => i.Estado == "Cerrada");

            PorPrioridad = incidencias
                .GroupBy(i => i.Prioridad)
                .ToDictionary(g => g.Key, g => g.Count());

            // Tiempo medio de resolución
            var resueltas = incidencias
                .Where(i => i.Estado == "Resuelta" || i.Estado == "Cerrada")
                .Where(i => i.FechaActualizacion.HasValue)
                .ToList();

            if (resueltas.Any())
            {
                var media = resueltas
                    .Average(i => (i.FechaActualizacion!.Value - i.FechaCreacion).TotalHours);
                TiempoMedioResolucion = $"{Math.Round(media, 1)} horas";
            }

            // Técnico con más carga
            var conTecnico = incidencias
                .Where(i => i.TecnicoAsignadoId.HasValue &&
                            (i.Estado == "Abierta" || i.Estado == "EnProceso"))
                .GroupBy(i => i.TecnicoAsignadoId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (conTecnico != null)
                TecnicoMasCarga = $"Técnico ID {conTecnico.Key} ({conTecnico.Count()} incidencias)";
        }

        return Page();
    }
}