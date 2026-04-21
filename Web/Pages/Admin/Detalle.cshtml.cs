using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Admin;

public class DetalleModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public IncidenciaDetalleModel? Incidencia { get; set; }
    public List<UsuarioModel> Tecnicos { get; set; } = new();

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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

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
            Tecnicos = todos.Where(u => u.Rol == "Tecnico").ToList();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, string nuevoEstado)
    {
        var client = GetClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { estado = nuevoEstado }),
            Encoding.UTF8, "application/json");
        await client.PutAsync($"/api/Incidencias/{id}", body);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAsignarTecnicoAsync(int id, int? tecnicoId)
    {
        var client = GetClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { tecnicoAsignadoId = tecnicoId }),
            Encoding.UTF8, "application/json");
        await client.PutAsync($"/api/Incidencias/{id}", body);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostComentarAsync(int incidenciaId, string contenido)
    {
        var client = GetClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { contenido, incidenciaId }),
            Encoding.UTF8, "application/json");
        await client.PostAsync("/api/Comentarios", body);
        return RedirectToPage(new { id = incidenciaId });
    }
}