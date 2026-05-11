using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

public class ComentarioDto
{
    [Required(ErrorMessage = "El contenido es obligatorio.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "El comentario no puede estar vacío.")]
    public string Contenido { get; set; } = string.Empty;

    [Required]
    public int IncidenciaId { get; set; }
}