using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task EnviarAsignacionAsync(string tecnicoEmail, string tecnicoNombre,
        int incidenciaId, string titulo, string descripcion,
        string prioridad, string? categoria)
    {
        try
        {
            var smtp   = _config["Email:Smtp"]!;
            var port   = int.Parse(_config["Email:Port"] ?? "587");
            var user   = _config["Email:Usuario"]!;
            var pass   = _config["Email:Password"]!;
            var remite = _config["Email:Remitente"] ?? "noreply@incidencias.local";

            var colorPrioridad = prioridad switch
            {
                "Critica" => "#DC3545",
                "Alta"    => "#FFC107",
                "Media"   => "#0D6EFD",
                _         => "#6C757D"
            };

            var catBadge = categoria != null
                ? $"<span class=\"badge\" style=\"background:#0DCAF0;color:#212529\">{categoria}</span>"
                : "";

            var htmlBody = $@"<!DOCTYPE html>
<html lang=""es"">
<head><meta charset=""utf-8"">
<style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
           background:#F0F2F5; margin:0; padding:32px; color:#374151 }}
    .card {{ background:white; border-radius:12px; padding:32px; max-width:520px;
             margin:0 auto; box-shadow:0 4px 16px rgba(0,0,0,.08) }}
    .header {{ background:#161D35; border-radius:10px; padding:20px 24px;
               margin-bottom:24px }}
    .badge-logo {{ background:#0D6EFD; border-radius:9px; width:40px; height:40px;
                   display:inline-flex; align-items:center; justify-content:center;
                   font-size:22px; font-weight:900; color:white }}
    .header-title {{ font-size:16px; font-weight:700; margin:0; color:white }}
    .header-sub {{ font-size:12px; color:#8899B4; margin:3px 0 0 }}
    h2 {{ font-size:20px; margin:0 0 8px; color:#161D35 }}
    .desc {{ color:#6B7280; font-size:14px; line-height:1.6; margin-bottom:20px }}
    .badges {{ display:flex; gap:8px; flex-wrap:wrap; margin-bottom:20px }}
    .badge {{ padding:4px 12px; border-radius:5px; font-size:12px; font-weight:600; color:white }}
    .divider {{ border:none; border-top:1px solid #F0F2F5; margin:20px 0 }}
    .footer {{ font-size:12px; color:#9AA3B0; text-align:center; margin-top:24px }}
</style>
</head>
<body>
<div class=""card"">
    <div class=""header"">
        <span class=""badge-logo"">!</span>
        <p class=""header-title"">Gestión de Incidencias</p>
        <p class=""header-sub"">Notificación automática</p>
    </div>

    <p style=""font-size:14px;color:#6B7280;margin-bottom:16px"">
        Hola <strong>{tecnicoNombre}</strong>, se te ha asignado la siguiente incidencia:
    </p>

    <h2>#{incidenciaId} — {titulo}</h2>
    <p class=""desc"">{descripcion}</p>

    <div class=""badges"">
        <span class=""badge"" style=""background:{colorPrioridad}"">{prioridad}</span>
        {catBadge}
    </div>

    <hr class=""divider"">
    <p style=""font-size:13px;color:#374151"">
        Accede al sistema para ver todos los detalles y actualizar el estado.
    </p>

    <p class=""footer"">Este mensaje ha sido generado automáticamente por el sistema de gestión de incidencias.</p>
</div>
</body></html>";

            var mensaje = new MimeMessage();
            mensaje.From.Add(MailboxAddress.Parse(remite));
            mensaje.To.Add(new MailboxAddress(tecnicoNombre, tecnicoEmail));
            mensaje.Subject = $"[Incidencia #{incidenciaId}] Se te ha asignado una nueva incidencia";
            mensaje.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(mensaje);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email de asignación enviado a {Email} para incidencia #{Id}",
                tecnicoEmail, incidenciaId);
        }
        catch (Exception ex)
        {
            // El email no es crítico — si falla, la asignación sigue adelante
            _logger.LogWarning("No se pudo enviar el email de asignación: {Error}", ex.Message);
        }
    }
}
