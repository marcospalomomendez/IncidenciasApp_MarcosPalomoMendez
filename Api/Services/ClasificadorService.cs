using System.Net.Http.Json;
using System.Text.Json;

namespace Api.Services;

public class ClasificadorService : IClasificadorService
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ClasificadorService> _logger;

    public ClasificadorService(IHttpClientFactory factory, ILogger<ClasificadorService> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    public async Task<ClasificacionResult?> ClasificarAsync(string titulo, string descripcion)
    {
        try
        {
            var http = _factory.CreateClient("Groq");

            var prompt = $$"""
                Analiza esta incidencia técnica de soporte IT y responde ÚNICAMENTE con un JSON válido, sin texto adicional, sin markdown, sin explicaciones:
                {"categoria": "X", "prioridad": "Y", "justificacion": "Z"}

                Valores válidos para categoria: Hardware, Software, Red, Acceso, Otro
                Valores válidos para prioridad: Baja, Media, Alta, Critica

                Criterios de prioridad:
                - Critica: sistema caído, pérdida de datos, afecta a toda la empresa
                - Alta: problema grave que impide trabajar a una persona
                - Media: problema que dificulta el trabajo pero tiene solución temporal
                - Baja: mejora o problema menor sin urgencia

                Título: {{titulo}}
                Descripción: {{descripcion}}
                """;

            var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            req.Content = JsonContent.Create(new
            {
                model    = "llama-3.1-8b-instant",
                max_tokens = 200,
                messages = new[] { new { role = "user", content = prompt } }
            });

            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning("Groq devolvió {StatusCode}: {Body}", (int)resp.StatusCode, err);
                return null;
            }

            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogDebug("Respuesta Groq: {Body}", body);

            var json = JsonSerializer.Deserialize<JsonElement>(body);
            var text = json.GetProperty("choices")[0]
                          .GetProperty("message")
                          .GetProperty("content")
                          .GetString()?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("Groq devolvió contenido vacío");
                return null;
            }

            // Extraer el bloque JSON aunque venga envuelto en markdown
            var start = text.IndexOf('{');
            var end   = text.LastIndexOf('}');
            if (start < 0 || end < 0)
            {
                _logger.LogWarning("No se encontró JSON en la respuesta de Groq: {Text}", text);
                return null;
            }
            text = text[start..(end + 1)];

            var resultado  = JsonSerializer.Deserialize<JsonElement>(text);
            var categoria  = resultado.GetProperty("categoria").GetString() ?? "Otro";
            var prioridad  = resultado.TryGetProperty("prioridad",   out var p) ? p.GetString() : null;
            var justif     = resultado.TryGetProperty("justificacion", out var j) ? j.GetString() : null;

            if (categoria is not ("Hardware" or "Software" or "Red" or "Acceso" or "Otro"))
                categoria = "Otro";
            if (prioridad is not (null or "Baja" or "Media" or "Alta" or "Critica"))
                prioridad = null;

            _logger.LogInformation("Clasificación IA: {Categoria} / {Prioridad}", categoria, prioridad);
            return new ClasificacionResult(categoria, prioridad, justif);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error llamando a Groq para clasificar incidencia");
            return null;
        }
    }
}
