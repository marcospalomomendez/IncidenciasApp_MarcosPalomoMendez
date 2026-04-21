using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Tecnico;

public class DetalleModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public IncidenciaDetalleModel? Incidencia { get; set; }

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
        var response = await client.GetAsync($"/api/Incidencias/{id}");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            Incidencia = JsonSerializer.Deserialize<IncidenciaDetalleModel>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsignarAsync(int id)
    {
        var client = GetClient();
        await client.PutAsync($"/api/Incidencias/{id}/asignar", null);
        return RedirectToPage(new { id });
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