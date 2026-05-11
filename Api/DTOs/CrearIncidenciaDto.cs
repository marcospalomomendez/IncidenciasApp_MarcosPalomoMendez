using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

public class CrearIncidenciaDto
{
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "El título debe tener entre 3 y 200 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 2000 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La prioridad es obligatoria.")]
    [RegularExpression("^(Baja|Media|Alta|Critica)$", ErrorMessage = "Prioridad no válida.")]
    public string Prioridad { get; set; } = "Media";
}