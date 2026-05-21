namespace Web.Models;

public class AuditoriaEntryModel
{
    public int Id { get; set; }
    public string Campo { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }

    public string Descripcion => Campo switch
    {
        "Creación" => $"Creada por {UsuarioNombre}",
        "Estado"   => $"{ValorAnterior} → {ValorNuevo}",
        "Técnico"  => $"Técnico: {ValorNuevo ?? "Sin asignar"}",
        _          => $"{Campo}: {ValorAnterior} → {ValorNuevo}"
    };

    public string IconColor => Campo switch
    {
        "Creación" => "#198754",
        "Estado"   => "#0D6EFD",
        "Técnico"  => "#D97706",
        _          => "#6C757D"
    };

    public string Icon => Campo switch
    {
        "Creación" => "✚",
        "Estado"   => "↺",
        "Técnico"  => "⟳",
        _          => "•"
    };
}
