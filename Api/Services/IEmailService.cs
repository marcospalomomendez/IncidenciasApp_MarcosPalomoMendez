namespace Api.Services;

public interface IEmailService
{
    Task EnviarAsignacionAsync(string tecnicoEmail, string tecnicoNombre,
        int incidenciaId, string titulo, string descripcion,
        string prioridad, string? categoria);
}