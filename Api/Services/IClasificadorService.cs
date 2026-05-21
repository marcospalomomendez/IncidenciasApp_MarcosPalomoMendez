namespace Api.Services;

public record ClasificacionResult(string Categoria, string? Prioridad, string? Justificacion);

public interface IClasificadorService
{
    Task<ClasificacionResult?> ClasificarAsync(string titulo, string descripcion);
}
