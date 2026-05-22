using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shared;
using Web.Models;

namespace Web.Pages.Admin;

public class DetalleModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public IncidenciaDetalleModel? Incidencia { get; set; }
    public List<UsuarioModel> Tecnicos { get; set; } = new();
    public List<AuditoriaEntryModel> Auditoria { get; set; } = new();
    public bool EsSuscrito { get; set; }
    public string ReturnUrl { get; set; } = "/Admin/Incidencias";

    public DetalleModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient GetClient()
    {
        var token = HttpContext.Session.GetString("Token");
        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!);
        return client;
    }

    public async Task<IActionResult> OnGetAsync(int id, string returnUrl = "")
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        ReturnUrl = string.IsNullOrEmpty(returnUrl) ? "/Admin/Incidencias" : returnUrl;

        var client = GetClient();

        var responseIncidencia = await client.GetAsync($"/api/Incidencias/{id}");
        if (responseIncidencia.IsSuccessStatusCode)
        {
            var json = await responseIncidencia.Content.ReadAsStringAsync();
            Incidencia = JsonSerializer.Deserialize<IncidenciaDetalleModel>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        var responseUsuarios = await client.GetAsync("/api/Usuarios");
        if (responseUsuarios.IsSuccessStatusCode)
        {
            var json = await responseUsuarios.Content.ReadAsStringAsync();
            var todos = JsonSerializer.Deserialize<List<UsuarioModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            Tecnicos = todos.Where(u => u.Rol == Roles.Tecnico).ToList();
        }

        var responseAuditoria = await client.GetAsync($"/api/Incidencias/{id}/auditoria");
        if (responseAuditoria.IsSuccessStatusCode)
        {
            var jsonA = await responseAuditoria.Content.ReadAsStringAsync();
            Auditoria = JsonSerializer.Deserialize<List<AuditoriaEntryModel>>(jsonA,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        var resSuscrito = await client.GetAsync($"/api/Incidencias/{id}/suscrito");
        if (resSuscrito.IsSuccessStatusCode)
        {
            var jsonS = await resSuscrito.Content.ReadAsStringAsync();
            var obj = JsonSerializer.Deserialize<JsonElement>(jsonS);
            EsSuscrito = obj.GetProperty("suscrito").GetBoolean();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSuscribirAsync(int id, string returnUrl = "")
    {
        await GetClient().PostAsync($"/api/Incidencias/{id}/suscribir", null);
        return RedirectToPage(new { id, returnUrl });
    }

    public async Task<IActionResult> OnPostDesuscribirAsync(int id, string returnUrl = "")
    {
        await GetClient().DeleteAsync($"/api/Incidencias/{id}/suscribir");
        return RedirectToPage(new { id, returnUrl });
    }

    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, string nuevoEstado, string returnUrl = "")
    {
        var client = GetClient();

        // Primero obtener la incidencia para no perder el técnico
        var resInc = await client.GetAsync($"/api/Incidencias/{id}");
        int? tecnicoActual = null;
        if (resInc.IsSuccessStatusCode)
        {
            var incJson = await resInc.Content.ReadAsStringAsync();
            var inc = JsonSerializer.Deserialize<IncidenciaDetalleModel>(incJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            tecnicoActual = inc?.TecnicoAsignadoId;
        }

        var body = new StringContent(
            JsonSerializer.Serialize(new { estado = nuevoEstado, tecnicoAsignadoId = tecnicoActual }),
            Encoding.UTF8, "application/json");

        await client.PutAsync($"/api/Incidencias/{id}", body);
        return RedirectToPage(new { id, returnUrl });
    }

    public async Task<IActionResult> OnPostAsignarTecnicoAsync(int id, int? tecnicoId, string returnUrl = "")
    {
        var client = GetClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { tecnicoAsignadoId = tecnicoId }),
            Encoding.UTF8, "application/json");

        await client.PutAsync($"/api/Incidencias/{id}", body);
        return RedirectToPage(new { id, returnUrl });
    }

    public async Task<IActionResult> OnPostComentarAsync(int incidenciaId, string contenido, string returnUrl = "")
    {
        var client = GetClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { contenido, incidenciaId }),
            Encoding.UTF8, "application/json");
        await client.PostAsync("/api/Comentarios", body);
        return RedirectToPage(new { id = incidenciaId, returnUrl });
    }
}