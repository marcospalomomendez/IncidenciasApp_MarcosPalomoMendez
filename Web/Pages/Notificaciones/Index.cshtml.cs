using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;
using Shared;

namespace Web.Pages.Notificaciones;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public List<NotifItem> Notificaciones { get; set; } = new();
    public string RolUsuario { get; set; } = "Usuario";

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

        RolUsuario = HttpContext.Session.GetString("Rol") ?? "Usuario";

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/Notificaciones");
        if (res.IsSuccessStatusCode)
        {
            var json = await res.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<NotifResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Notificaciones = data?.Notificaciones ?? new();
        }

        return Page();
    }

    // GET ?handler=Count — para el badge del navbar (llamado via JS fetch)
    public async Task<IActionResult> OnGetCountAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return new JsonResult(new { noLeidas = 0 });

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/Notificaciones");
        if (!res.IsSuccessStatusCode) return new JsonResult(new { noLeidas = 0 });

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var count = doc.RootElement.GetProperty("noLeidas").GetInt32();
        return new JsonResult(new { noLeidas = count });
    }

    public async Task<IActionResult> OnPostLeerAsync(int id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.DeleteAsync($"/api/Notificaciones/{id}");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLeerTodasAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.DeleteAsync("/api/Notificaciones/todas");

        return RedirectToPage();
    }

    public class NotifResponse
    {
        public int NoLeidas { get; set; }
        public List<NotifItem> Notificaciones { get; set; } = new();
    }

    public class NotifItem
    {
        public int Id { get; set; }
        public string Mensaje { get; set; } = "";
        public bool Leida { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int? IncidenciaId { get; set; }
    }
}
