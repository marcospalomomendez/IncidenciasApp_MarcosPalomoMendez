using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Usuario;

public class DetalleModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public IncidenciaDetalleModel? Incidencia { get; set; }

    public DetalleModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/Incidencias/{id}");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            Incidencia = JsonSerializer.Deserialize<IncidenciaDetalleModel>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int incidenciaId, string contenido)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var body = new StringContent(
            JsonSerializer.Serialize(new { contenido, incidenciaId }),
            Encoding.UTF8, "application/json");

        await client.PostAsync("/api/Comentarios", body);

        return RedirectToPage(new { id = incidenciaId });
    }
}