namespace Api.Services;

public record ClasificacionResult(string Categoria, string? Prioridad, string? Justificacion);

public interface IClasificadorService
{
    Task<ClasificacionResult?> ClasificarAsync(string titulo, string descripcion);
    Task<string?> SugerirSolucionAsync(string titulo, string descripcion, string categoria);
    Task<string?> ConsultarLibreAsync(string pregunta, string contexto);
}
