using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;
using Shared;
using Web.Models;

namespace Web.Pages.Usuario;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public List<IncidenciaModel> Incidencias { get; set; } = new();
    public int PaginaActual  { get; set; } = 1;
    public int TotalPaginas  { get; set; } = 1;
    public int Total         { get; set; } = 0;
    public string FiltroEstado    { get; set; } = "Todas";
    public string Orden           { get; set; } = "desc";
    public string FiltroCategoria { get; set; } = "";
    public string FiltroPrioridad { get; set; } = "";
    public string BusquedaQ       { get; set; } = "";

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string UrlFiltro(string? estado = null, string? orden = null,
        string? categoria = null, string? prioridad = null, string? q = null, int pagina = 1)
    {
        var e  = estado    ?? FiltroEstado;
        var o  = orden     ?? Orden;
        var c  = categoria ?? FiltroCategoria;
        var p  = prioridad ?? FiltroPrioridad;
        var qv = Uri.EscapeDataString(q ?? BusquedaQ ?? "");
        return $"?estado={e}&orden={o}&categoria={c}&prioridad={p}&q={qv}&pagina={pagina}";
    }

    public async Task<IActionResult> OnGetAsync(int pagina = 1,
        string estado = "Todas", string orden = "desc",
        string categoria = "", string prioridad = "", string q = "")
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");
        var rol = HttpContext.Session.GetString("Rol");
        if (rol != Roles.Usuario && rol != Roles.Admin) return RedirectToPage("/Index");

        PaginaActual    = pagina;
        FiltroEstado    = estado;
        Orden           = orden;
        FiltroCategoria = categoria;
        FiltroPrioridad = prioridad;
        BusquedaQ       = q ?? "";

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"/api/Incidencias/mis?pagina={pagina}&tamanio=10&orden={orden}";
        if (estado != "Todas")     url += $"&estado={estado}";
        if (!string.IsNullOrEmpty(categoria)) url += $"&categoria={categoria}";
        if (!string.IsNullOrEmpty(prioridad)) url += $"&prioridad={prioridad}";
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
}
