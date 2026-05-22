using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shared;
using Web.Models;

namespace Web.Pages.Tecnico;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public List<IncidenciaModel> Incidencias { get; set; } = new();
    public int    StatsActivas           { get; set; }
    public int    StatsResueltasSemana   { get; set; }
    public double? StatsTiempoMedio      { get; set; }
    public double? StatsSlaPct           { get; set; }
    public int PaginaActual  { get; set; } = 1;
    public int TotalPaginas  { get; set; } = 1;
    public int Total         { get; set; } = 0;
    public string FiltroEstado    { get; set; } = "Todas";
    public string Orden           { get; set; } = "desc";
    public string FiltroCategoria { get; set; } = "";
    public string FiltroPrioridad { get; set; } = "";
    public bool   FiltroSla        { get; set; } = false;
    public bool   FiltroSinAsignar { get; set; } = false;
    public bool   FiltroMias       { get; set; } = false;
    public string BusquedaQ        { get; set; } = "";

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string UrlFiltro(string? estado = null, string? orden = null,
        string? categoria = null, string? prioridad = null,
        bool? sla = null, bool? sinAsignar = null, bool? mias = null, string? q = null, int pagina = 1)
    {
        var e  = estado     ?? FiltroEstado;
        var o  = orden      ?? Orden;
        var c  = categoria  ?? FiltroCategoria;
        var p  = prioridad  ?? FiltroPrioridad;
        var s  = sla        ?? FiltroSla;
        var sa = sinAsignar ?? FiltroSinAsignar;
        var m  = mias       ?? FiltroMias;
        var qv = Uri.EscapeDataString(q ?? BusquedaQ ?? "");
        return $"?estado={e}&orden={o}&categoria={c}&prioridad={p}&sla={s}&sinAsignar={sa}&mias={m}&q={qv}&pagina={pagina}";
    }

    public async Task<IActionResult> OnGetAsync(int pagina = 1,
        string estado = "Todas", string orden = "desc",
        string categoria = "", string prioridad = "",
        bool sla = false, bool sinAsignar = false, bool mias = false, string q = "")
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");
        var rol = HttpContext.Session.GetString("Rol");
        if (rol != Roles.Tecnico && rol != Roles.Admin) return RedirectToPage("/Index");

        PaginaActual     = pagina;
        FiltroEstado     = estado;
        Orden            = orden;
        FiltroCategoria  = categoria;
        FiltroPrioridad  = prioridad;
        FiltroSla        = sla;
        FiltroSinAsignar = sinAsignar;
        FiltroMias       = mias;
        BusquedaQ        = q ?? "";

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"/api/Incidencias/panel-tecnico?pagina={pagina}&tamanio=10&orden={orden}";
        if (estado != "Todas")              url += $"&estado={estado}";
        if (!string.IsNullOrEmpty(categoria)) url += $"&categoria={categoria}";
        if (!string.IsNullOrEmpty(prioridad)) url += $"&prioridad={prioridad}";
        if (sla)        url += "&slaExcedido=true";
        if (sinAsignar) url += "&soloSinAsignar=true";
        if (mias)       url += "&soloAsignadas=true";
        if (!string.IsNullOrEmpty(q)) url += $"&q={Uri.EscapeDataString(q)}";

        var statsRes = await client.GetAsync("/api/Incidencias/mis-stats");
        if (statsRes.IsSuccessStatusCode)
        {
            var js = await statsRes.Content.ReadAsStringAsync();
            var s  = JsonSerializer.Deserialize<JsonElement>(js,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            StatsActivas         = s.GetProperty("activasAsignadas").GetInt32();
            StatsResueltasSemana = s.GetProperty("resueltasEstaSemana").GetInt32();
            if (s.TryGetProperty("tiempoMedioHoras", out var th) && th.ValueKind != JsonValueKind.Null)
                StatsTiempoMedio = th.GetDouble();
            if (s.TryGetProperty("slaCumplidoPct", out var sp) && sp.ValueKind != JsonValueKind.Null)
                StatsSlaPct = sp.GetDouble();
        }

        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var resultado = JsonSerializer.Deserialize<PaginadoModel<IncidenciaModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Incidencias  = resultado?.Datos        ?? new();
            TotalPaginas = resultado?.TotalPaginas ?? 1;
            Total        = resultado?.Total        ?? 0;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string nuevoEstado,
        string estado = "Todas", string orden = "desc",
        string categoria = "", string prioridad = "",
        bool sla = false, bool sinAsignar = false, bool mias = false, string q = "")
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new StringContent(
            JsonSerializer.Serialize(new { estado = nuevoEstado }),
            Encoding.UTF8, "application/json");
        await client.PutAsync($"/api/Incidencias/{id}", body);

        return await OnGetAsync(1, estado, orden, categoria, prioridad, sla, sinAsignar, mias, q);
    }
}
