namespace Web.Models
{
    public class HistorialEstadoModel
    {
        public int Id { get; set; }
        public string EstadoAnterior { get; set; } = string.Empty;
        public string EstadoNuevo { get; set; } = string.Empty;
        public DateTime FechaCambio { get; set; }
        public int UsuarioId { get; set; }
        public int IncidenciaId { get; set; }
    }
}
