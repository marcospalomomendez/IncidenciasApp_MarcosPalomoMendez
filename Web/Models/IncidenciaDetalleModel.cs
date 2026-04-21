namespace Web.Models
{
    public class IncidenciaDetalleModel
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Prioridad { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaActualizacion { get; set; }
        public int UsuarioCreadorId { get; set; }
        public int? TecnicoAsignadoId { get; set; }
        public List<ComentarioModel> Comentarios { get; set; } = new();
        public List<HistorialEstadoModel> Historial { get; set; } = new();
    }
}
