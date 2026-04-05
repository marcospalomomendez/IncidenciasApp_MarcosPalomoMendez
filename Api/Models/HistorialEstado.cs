namespace Api.Models;

public class HistorialEstado
{
    public int Id { get; set; }
    public string EstadoAnterior { get; set; } = string.Empty;
    public string EstadoNuevo { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; } = DateTime.Now;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int IncidenciaId { get; set; }
    public Incidencia Incidencia { get; set; } = null!;
}