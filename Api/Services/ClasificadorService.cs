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

    private async Task<string?> LlamarGroqAsync(string prompt, int maxTokens = 400)
    {
        try
        {
            var http = _factory.CreateClient("Groq");
            var req  = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            req.Content = JsonContent.Create(new
            {
                model      = "llama-3.1-8b-instant",
                max_tokens = maxTokens,
                messages   = new[] { new { role = "user", content = prompt } }
            });

            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning("Groq {StatusCode}: {Body}", (int)resp.StatusCode, err);
                return null;
            }

            var body = await resp.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            return json.GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString()?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error llamando a Groq");
            return null;
        }
    }

    public async Task<ClasificacionResult?> ClasificarAsync(string titulo, string descripcion)
    {
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

        var text = await LlamarGroqAsync(prompt, 200);
        if (string.IsNullOrEmpty(text)) return null;

        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        if (start < 0 || end < 0) return null;
        text = text[start..(end + 1)];

        try
        {
            var resultado = JsonSerializer.Deserialize<JsonElement>(text);
            var categoria = resultado.GetProperty("categoria").GetString() ?? "Otro";
            var prioridad = resultado.TryGetProperty("prioridad",    out var p) ? p.GetString() : null;
            var justif    = resultado.TryGetProperty("justificacion", out var j) ? j.GetString() : null;

            if (categoria is not ("Hardware" or "Software" or "Red" or "Acceso" or "Otro"))
                categoria = "Otro";
            if (prioridad is not (null or "Baja" or "Media" or "Alta" or "Critica"))
                prioridad = null;

            _logger.LogInformation("Clasificación IA: {Categoria} / {Prioridad}", categoria, prioridad);
            return new ClasificacionResult(categoria, prioridad, justif);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> SugerirSolucionAsync(string titulo, string descripcion, string categoria)
    {
        var prompt = $"""
            Eres un técnico de soporte IT experto. Sugiere pasos concretos para resolver esta incidencia.
            Categoría: {categoria}
            Título: {titulo}
            Descripción: {descripcion}
            Da entre 3 y 5 pasos numerados en español. Sé breve y directo. Sin introducciones.
            """;

        return await LlamarGroqAsync(prompt, 350);
    }

    public async Task<string?> ConsultarLibreAsync(string pregunta, string contexto)
    {
        var prompt = $"""
            Eres un asistente de análisis exclusivo para un sistema de gestión de incidencias IT.
            Solo puedes responder preguntas sobre los datos del sistema que se te proporcionan a continuación.
            Si la pregunta no está relacionada con el sistema de incidencias, responde exactamente: "Solo puedo responder preguntas sobre el sistema de gestión de incidencias."

            Datos actuales del sistema:
            {contexto}

            Pregunta del administrador: {pregunta}
            Responde en español de forma concisa y útil. Usa **negrita** para destacar datos clave.
            """;

        return await LlamarGroqAsync(prompt, 400);
    }
}
