namespace Api.Models;

public class Incidencia
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = "Abierta"; // Abierta, EnProceso, Resuelta, Cerrada
    public string Prioridad { get; set; } = "Media"; // Baja, Media, Alta, Critica
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaActualizacion { get; set; }
    public int UsuarioCreadorId { get; set; }
    public Usuario UsuarioCreador { get; set; } = null!;
    public int? TecnicoAsignadoId { get; set; }
    public Usuario? TecnicoAsignado { get; set; }
    public List<Comentario> Comentarios { get; set; } = new();
    public List<HistorialEstado> Historial { get; set; } = new();
}