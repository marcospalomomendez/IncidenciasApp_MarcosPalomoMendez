namespace Api.Models;

public class Comentario
{
    public int Id { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int IncidenciaId { get; set; }
    public Incidencia Incidencia { get; set; } = null!;
}