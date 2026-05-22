namespace Api.Models;

public class SuscripcionIncidencia
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int IncidenciaId { get; set; }
    public Incidencia Incidencia { get; set; } = null!;
    public DateTime FechaSuscripcion { get; set; } = DateTime.UtcNow;
}
