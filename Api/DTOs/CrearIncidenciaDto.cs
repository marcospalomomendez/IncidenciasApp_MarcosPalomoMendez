namespace Api.DTOs
{
    public class CrearIncidenciaDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Prioridad { get; set; } = "Media";
    }
}
