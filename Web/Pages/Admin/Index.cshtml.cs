using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;
using Shared;
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
        if (rol != Roles.Admin)
            return RedirectToPage("/Index");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Incidencias?pagina=1&tamanio=1000");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var resultado = JsonSerializer.Deserialize<PaginadoModel<IncidenciaModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var incidencias = resultado?.Datos ?? new();

            TotalIncidencias = resultado?.Total ?? 0;
            Abiertas = incidencias.Count(i => i.Estado == "Abierta");
            EnProceso = incidencias.Count(i => i.Estado == "EnProceso");
            Cerradas = incidencias.Count(i => i.Estado == "Cerrada");

            PorPrioridad = incidencias
                .GroupBy(i => i.Prioridad)
                .ToDictionary(g => g.Key, g => g.Count());
           
            // Tiempo medio de resoluci�n
            var resueltas = incidencias
                .Where(i => i.Estado == "Resuelta" || i.Estado == "Cerrada")
                .Where(i => i.FechaActualizacion.HasValue)
                .ToList();

            if (resueltas.Any())
            {
                var tiempos = resueltas
                    .Select(i => (i.FechaActualizacion!.Value - i.FechaCreacion).TotalHours)
                    .Where(h => h > 0)
                    .ToList();

                if (tiempos.Any())
                    TiempoMedioResolucion = $"{Math.Round(tiempos.Average(), 1)} horas";
            }

            // T�cnico con m�s carga
            var conTecnico = incidencias
                .Where(i => i.TecnicoAsignadoId.HasValue &&
                            (i.Estado == "Abierta" || i.Estado == "EnProceso"))
                .GroupBy(i => i.TecnicoAsignadoId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (conTecnico != null)
                TecnicoMasCarga = $"T�cnico ID {conTecnico.Key} ({conTecnico.Count()} incidencias)";
        }

        return Page();
    }
}