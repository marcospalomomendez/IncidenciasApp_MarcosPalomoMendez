using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Admin;

public class IncidenciasModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public List<IncidenciaModel> Incidencias { get; set; } = new();
    public string Mensaje { get; set; } = string.Empty;

    public IncidenciasModel(IHttpClientFactory httpClientFactory)
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
            Incidencias = JsonSerializer.Deserialize<List<IncidenciaModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string nuevoEstado, string accion)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

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

        return await OnGetAsync();
    }
}