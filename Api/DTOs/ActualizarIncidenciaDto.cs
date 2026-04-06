namespace Api.DTOs
{
    public class ActualizarIncidenciaDto
    {
        public string Estado { get; set; } = string.Empty;
        public int? TecnicoAsignadoId { get; set; }
    }
}
