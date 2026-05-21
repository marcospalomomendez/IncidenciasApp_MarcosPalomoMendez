using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shared;
using Web.Models;

namespace Web.Pages.Admin;

public class IncidenciasModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public List<IncidenciaModel> Incidencias { get; set; } = new();
    public string Mensaje { get; set; } = string.Empty;
    public int PaginaActual  { get; set; } = 1;
    public int TotalPaginas  { get; set; } = 1;
    public int Total         { get; set; } = 0;
    public string FiltroEstado    { get; set; } = "Todas";
    public string Orden           { get; set; } = "desc";
    public string FiltroCategoria { get; set; } = "";
    public string FiltroPrioridad { get; set; } = "";
    public bool   FiltroSla       { get; set; } = false;
    public bool   FiltroSinAsignar { get; set; } = false;
    public string BusquedaQ       { get; set; } = "";

    public IncidenciasModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string UrlFiltro(string? estado = null, string? orden = null,
        string? categoria = null, string? prioridad = null,
        bool? sla = null, bool? sinAsignar = null, string? q = null, int pagina = 1)
    {
        var e  = estado     ?? FiltroEstado;
        var o  = orden      ?? Orden;
        var c  = categoria  ?? FiltroCategoria;
        var p  = prioridad  ?? FiltroPrioridad;
        var s  = sla        ?? FiltroSla;
        var sa = sinAsignar ?? FiltroSinAsignar;
        var qv = Uri.EscapeDataString(q ?? BusquedaQ);
        return $"?estado={e}&orden={o}&categoria={c}&prioridad={p}&sla={s}&sinAsignar={sa}&q={qv}&pagina={pagina}";
    }

    public async Task<IActionResult> OnGetAsync(int pagina = 1,
        string estado = "Todas", string orden = "desc",
        string categoria = "", string prioridad = "",
        bool sla = false, bool sinAsignar = false, string q = "")
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");
        if (HttpContext.Session.GetString("Rol") != Roles.Admin) return RedirectToPage("/Index");

        PaginaActual    = pagina;
        FiltroEstado    = estado;
        Orden           = orden;
        FiltroCategoria = categoria;
        FiltroPrioridad = prioridad;
        FiltroSla       = sla;
        FiltroSinAsignar = sinAsignar;
        BusquedaQ       = q;

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"/api/Incidencias?pagina={pagina}&tamanio=10&orden={orden}";
        if (estado != "Todas")     url += $"&estado={estado}";
        if (!string.IsNullOrEmpty(categoria)) url += $"&categoria={categoria}";
        if (!string.IsNullOrEmpty(prioridad)) url += $"&prioridad={prioridad}";
        if (sla)        url += "&slaExcedido=true";
        if (sinAsignar) url += "&sinAsignar=true";
        if (!string.IsNullOrEmpty(q)) url += $"&q={Uri.EscapeDataString(q)}";

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

    public async Task<IActionResult> OnPostAsync(int id, string nuevoEstado, string accion,
        string estado = "Todas", string orden = "desc",
        string categoria = "", string prioridad = "",
        bool sla = false, bool sinAsignar = false, string q = "")
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (accion == "eliminar")
        {
            await client.DeleteAsync($"/api/Incidencias/{id}");
            Mensaje = "Incidencia eliminada correctamente.";
        }
        else
        {
            var body = new StringContent(
                JsonSerializer.Serialize(new { estado = nuevoEstado }),
                Encoding.UTF8, "application/json");
            await client.PutAsync($"/api/Incidencias/{id}", body);
            Mensaje = "Estado actualizado correctamente.";
        }

        return await OnGetAsync(1, estado, orden, categoria, prioridad, sla, sinAsignar, q);
    }
}
