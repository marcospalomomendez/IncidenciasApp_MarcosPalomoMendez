using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shared;

namespace Web.Pages.Admin;

public class AsistenteModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private const string SessionKey = "AsistenteChat";

    public List<(string Pregunta, string Respuesta)> Historial { get; set; } = new();

    public AsistenteModel(IHttpClientFactory http) => _http = http;

    public IActionResult OnGet()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");
        if (HttpContext.Session.GetString("Rol") != Roles.Admin) return RedirectToPage("/Index");

        CargarHistorial();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string tipo, string? categoria = null, string? preguntaLibre = null)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

        CargarHistorial();

        if (tipo == "limpiar")
        {
            HttpContext.Session.Remove(SessionKey);
            return RedirectToPage();
        }

        var client = _http.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string pregunta;
        string respuesta = "No se pudo obtener respuesta del servidor.";

        if (tipo == "libre")
        {
            pregunta = preguntaLibre?.Trim() ?? "";
            if (string.IsNullOrEmpty(pregunta)) return RedirectToPage();

            try
            {
                var body = new StringContent(
                    JsonSerializer.Serialize(new { pregunta }),
                    Encoding.UTF8, "application/json");
                var res = await client.PostAsync("/api/Incidencias/consulta-libre", body);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var doc = JsonSerializer.Deserialize<JsonElement>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (doc.TryGetProperty("respuesta", out var r))
                        respuesta = r.GetString() ?? respuesta;
                }
            }
            catch { }
        }
        else
        {
            pregunta = tipo switch
            {
                "tecnico-mas-activas"             => "¿Cuál es el técnico con más incidencias activas?",
                "tecnico-mas-resueltas"           => "¿Cuál es el técnico con más incidencias resueltas?",
                "tecnico-mas-resueltas-categoria" => $"¿Cuál es el técnico con más resueltas de categoría {categoria}?",
                "categoria-mas-incidencias"       => "¿Cuál es la categoría con más incidencias?",
                "sin-asignar"                     => "¿Cuántas incidencias están sin asignar?",
                "resumen-estados"                 => "¿Cómo está el resumen de estados actual?",
                "sla-excedido"                    => "¿Qué incidencias tienen el SLA excedido?",
                "tiempo-medio"                    => "¿Cuál es el tiempo medio de resolución?",
                _                                 => tipo
            };

            try
            {
                var url = $"/api/Incidencias/consulta?tipo={Uri.EscapeDataString(tipo)}";
                if (!string.IsNullOrEmpty(categoria)) url += $"&categoria={Uri.EscapeDataString(categoria)}";
                var res = await client.GetAsync(url);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var doc = JsonSerializer.Deserialize<JsonElement>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (doc.TryGetProperty("respuesta", out var r))
                        respuesta = r.GetString() ?? respuesta;
                }
            }
            catch { }
        }

        Historial.Add((pregunta, respuesta));
        GuardarHistorial();
        return RedirectToPage();
    }

    public string FormatearRespuesta(string texto)
    {
        var encoded = System.Net.WebUtility.HtmlEncode(texto);
        var bolded  = Regex.Replace(encoded, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        return bolded.Replace("\n", "<br>");
    }

    private void CargarHistorial()
    {
        var json = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<string[]>>(json);
            if (list != null)
                Historial = list.Select(x => (x[0], x[1])).ToList();
        }
        catch { }
    }

    private void GuardarHistorial()
    {
        var data = Historial.Select(x => new[] { x.Pregunta, x.Respuesta }).ToList();
        HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(data));
    }
}
