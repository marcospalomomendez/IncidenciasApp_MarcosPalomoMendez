namespace Web.Models
{
    public class ComentarioModel
    {
        public int Id { get; set; }
        public string Contenido { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public int UsuarioId { get; set; }
        public int IncidenciaId { get; set; }
    }
}
