namespace Api.Models;

public class AuditoriaEntry
{
    public int Id { get; set; }
    public int IncidenciaId { get; set; }
    public Incidencia Incidencia { get; set; } = null!;
    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string Campo { get; set; } = string.Empty;  // "Creación", "Estado", "Técnico"
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}