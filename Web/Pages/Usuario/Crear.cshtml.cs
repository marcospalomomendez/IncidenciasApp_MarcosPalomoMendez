using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Web.Pages.Usuario;

public class CrearModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public string Error { get; set; } = string.Empty;

    public CrearModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IActionResult OnGet()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string titulo, string descripcion, string prioridad = "Media")
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var body = new StringContent(
            JsonSerializer.Serialize(new { titulo, descripcion, prioridad }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/Incidencias", body);

        if (!response.IsSuccessStatusCode)
        {
            Error = "Error al crear la incidencia.";
            return Page();
        }

        var json = await response.Content.ReadAsStringAsync();
        var creada = JsonSerializer.Deserialize<JsonElement>(json);
        var iaDisponible = creada.TryGetProperty("justificacionIA", out var j) && j.ValueKind != JsonValueKind.Null;

        if (!iaDisponible)
            TempData["AvisoIA"] = "La clasificación automática no está disponible. Se usó la prioridad que seleccionaste manualmente.";

        return RedirectToPage("/Usuario/Index");
    }
}