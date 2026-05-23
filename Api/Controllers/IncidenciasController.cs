using Api.Data;
using Api.DTOs;
using Api.Hubs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Shared;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidenciasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IClasificadorService _clasificador;
    private readonly IHubContext<IncidenciasHub> _hub;
    private readonly IEmailService _email;

    public IncidenciasController(AppDbContext context, IClasificadorService clasificador,
        IHubContext<IncidenciasHub> hub, IEmailService email)
    {
        _context = context;
        _clasificador = clasificador;
        _hub = hub;
        _email = email;
    }

    private static bool EsSlaExcedido(Incidencia i)
    {
        if (i.Estado is Estados.Resuelta or Estados.Cerrada) return false;
        double horas = i.Prioridad switch
        {
            "Critica" => 2,
            "Alta"    => 8,
            "Media"   => 24,
            _         => 72
        };
        return (DateTime.UtcNow - i.FechaCreacion).TotalHours > horas;
    }

    private static object MapIncidencia(Incidencia i) => new
    { 
        i.Id,
        i.Titulo,
        i.Estado,
        i.Prioridad,
        i.Categoria,
        i.JustificacionIA,
        i.FechaCreacion,
        i.FechaActualizacion,
        i.UsuarioCreadorId,
        i.TecnicoAsignadoId,
        SlaExcedido = EsSlaExcedido(i)
    };

    private static IQueryable<Incidencia> AplicarFiltroQ(IQueryable<Incidencia> query, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return query;
        var term = q.Trim();
        return query.Where(i => i.Titulo.Contains(term) || i.Descripcion.Contains(term));
    }

    private async Task NotificarSuscriptoresAsync(int incidenciaId, int exceptoUsuarioId, string mensaje)
    {
        var suscriptores = await _context.Suscripciones
            .Where(s => s.IncidenciaId == incidenciaId && s.UsuarioId != exceptoUsuarioId)
            .Select(s => s.UsuarioId)
            .ToListAsync();

        foreach (var uid in suscriptores)
        {
            _context.Notificaciones.Add(new Notificacion
            {
                UsuarioId    = uid,
                Mensaje      = mensaje,
                IncidenciaId = incidenciaId
            });
            await _hub.Clients.Group($"user-{uid}")
                .SendAsync("NuevaNotificacion", mensaje);
        }
    }

    private async Task RegistrarAuditoria(int incidenciaId, int usuarioId,
        string campo, string? anterior, string? nuevo)
    {
        var nombre = await _context.Usuarios
            .Where(u => u.Id == usuarioId)
            .Select(u => u.Nombre)
            .FirstOrDefaultAsync() ?? $"#{usuarioId}";

        _context.Auditoria.Add(new AuditoriaEntry
        {
            IncidenciaId  = incidenciaId,
            UsuarioId     = usuarioId,
            UsuarioNombre = nombre,
            Campo         = campo,
            ValorAnterior = anterior,
            ValorNuevo    = nuevo
        });
    }

    // GET: api/Incidencias
    [HttpGet]
    public async Task<IActionResult> GetAll(int pagina = 1, int tamanio = 10,
        string? estado = null, bool sinAsignar = false,
        string orden = "desc", string? categoria = null,
        string? prioridad = null, bool slaExcedido = false, string? q = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamanio < 1 || tamanio > 1000) tamanio = 10;

        var query = _context.Incidencias
            .Include(i => i.UsuarioCreador)
            .Include(i => i.TecnicoAsignado)
            .AsQueryable();

        if (!string.IsNullOrEmpty(estado))
            query = query.Where(i => i.Estado == estado);
        if (sinAsignar)
            query = query.Where(i => i.TecnicoAsignadoId == null);
        if (!string.IsNullOrEmpty(categoria))
            query = query.Where(i => i.Categoria == categoria);
        if (!string.IsNullOrEmpty(prioridad))
            query = query.Where(i => i.Prioridad == prioridad);
        if (slaExcedido)
        {
            var ahora = DateTime.UtcNow;
            query = query.Where(i =>
                i.Estado != Estados.Resuelta && i.Estado != Estados.Cerrada && (
                    (i.Prioridad == "Critica" && i.FechaCreacion < ahora.AddHours(-2))  ||
                    (i.Prioridad == "Alta"    && i.FechaCreacion < ahora.AddHours(-8))  ||
                    (i.Prioridad == "Media"   && i.FechaCreacion < ahora.AddHours(-24)) ||
                    (i.Prioridad == "Baja"    && i.FechaCreacion < ahora.AddHours(-72))
                ));
        }
        query = AplicarFiltroQ(query, q);

        query = orden == "asc"
            ? query.OrderBy(i => i.FechaCreacion)
            : query.OrderByDescending(i => i.FechaCreacion);

        var total = await query.CountAsync();
        var incidencias = await query.Skip((pagina - 1) * tamanio).Take(tamanio).ToListAsync();

        return Ok(new
        {
            total,
            pagina,
            tamanio,
            totalPaginas = (int)Math.Ceiling((double)total / tamanio),
            datos = incidencias.Select(MapIncidencia)
        });
    }

    // GET: api/Incidencias/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var incidencia = await _context.Incidencias
            .Include(i => i.UsuarioCreador)
            .Include(i => i.TecnicoAsignado)
            .Include(i => i.Comentarios)
            .Include(i => i.Historial)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (incidencia == null) return NotFound();

        return Ok(new
        {
            incidencia.Id,
            incidencia.Titulo,
            incidencia.Descripcion,
            incidencia.Estado,
            incidencia.Prioridad,
            incidencia.Categoria,
            incidencia.FechaCreacion,
            incidencia.FechaActualizacion,
            incidencia.UsuarioCreadorId,
            incidencia.TecnicoAsignadoId,
            SlaExcedido = EsSlaExcedido(incidencia),
            Comentarios = incidencia.Comentarios.Select(c => new
            {
                c.Id, c.Contenido, c.FechaCreacion, c.UsuarioId, c.IncidenciaId
            }),
            Historial = incidencia.Historial.Select(h => new
            {
                h.Id, h.EstadoAnterior, h.EstadoNuevo, h.FechaCambio, h.UsuarioId, h.IncidenciaId
            })
        });
    }

    // GET: api/Incidencias/{id}/auditoria
    [HttpGet("{id}/auditoria")]
    public async Task<IActionResult> GetAuditoria(int id)
    {
        var entries = await _context.Auditoria
            .Where(a => a.IncidenciaId == id)
            .OrderByDescending(a => a.Fecha)
            .Select(a => new
            {
                a.Id, a.Campo, a.ValorAnterior, a.ValorNuevo, a.UsuarioNombre, a.Fecha
            })
            .ToListAsync();

        return Ok(entries);
    }

    // POST: api/Incidencias
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearIncidenciaDto dto)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var incidencia = new Incidencia
        {
            Titulo           = dto.Titulo,
            Descripcion      = dto.Descripcion,
            Prioridad        = dto.Prioridad,
            UsuarioCreadorId = usuarioId
        };
        _context.Incidencias.Add(incidencia);
        await _context.SaveChangesAsync();

        var clasificacion = await _clasificador.ClasificarAsync(dto.Titulo, dto.Descripcion);
        incidencia.Categoria       = clasificacion?.Categoria ?? "Otro";
        incidencia.JustificacionIA = clasificacion?.Justificacion;
        if (clasificacion?.Prioridad != null)
            incidencia.Prioridad = clasificacion.Prioridad;

        // Auditoría de creación
        await RegistrarAuditoria(incidencia.Id, usuarioId, "Creación", null, incidencia.Estado);

        // Notificar a todos los admins y técnicos
        var destinatarios = await _context.Usuarios
            .Where(u => u.Rol == Roles.Admin || u.Rol == Roles.Tecnico)
            .Select(u => u.Id)
            .ToListAsync();

        var mensaje = $"Nueva incidencia: \"{incidencia.Titulo}\" — Categoría: {incidencia.Categoria}";
        _context.Notificaciones.AddRange(destinatarios.Select(uid => new Notificacion
        {
            UsuarioId    = uid,
            Mensaje      = mensaje,
            IncidenciaId = incidencia.Id
        }));
        await _context.SaveChangesAsync();

        // SignalR: avisar a admins y técnicos en tiempo real
        await _hub.Clients.Group("admins-tecnicos")
            .SendAsync("NuevaIncidencia", incidencia.Id, incidencia.Titulo, incidencia.Categoria);

        return Ok(MapIncidencia(incidencia));
    }

    // PUT: api/Incidencias/{id}
    [HttpPut("{id}")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarIncidenciaDto dto)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia == null) return NotFound();

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (!string.IsNullOrEmpty(dto.Estado) &&
            dto.Estado != Estados.Abierta && dto.Estado != Estados.EnProceso &&
            dto.Estado != Estados.Resuelta && dto.Estado != Estados.Cerrada)
            return BadRequest("Estado no válido.");

        if (!string.IsNullOrEmpty(dto.Estado) && dto.Estado != incidencia.Estado)
        {
            var estadoAnterior = incidencia.Estado;
            _context.HistorialEstados.Add(new HistorialEstado
            {
                EstadoAnterior = estadoAnterior,
                EstadoNuevo    = dto.Estado,
                UsuarioId      = usuarioId,
                IncidenciaId   = id
            });
            incidencia.Estado = dto.Estado;

            await RegistrarAuditoria(id, usuarioId, "Estado", estadoAnterior, dto.Estado);

            // Notificación DB al creador
            _context.Notificaciones.Add(new Notificacion
            {
                UsuarioId    = incidencia.UsuarioCreadorId,
                Mensaje      = $"Tu incidencia #{id} cambió de estado: {estadoAnterior} → {dto.Estado}",
                IncidenciaId = id
            });

            // Notificar a suscriptores del cambio de estado
            await NotificarSuscriptoresAsync(id, usuarioId,
                $"[Seguimiento] Incidencia #{id} cambió a {dto.Estado}: {incidencia.Titulo}");

            // Si se marca como Resuelta, notificar a todos los admins para que la cierren
            if (dto.Estado == Estados.Resuelta)
            {
                var admins = await _context.Usuarios
                    .Where(u => u.Rol == Roles.Admin)
                    .Select(u => u.Id)
                    .ToListAsync();

                var tecnicoNombreResolucion = await _context.Usuarios
                    .Where(u => u.Id == usuarioId)
                    .Select(u => u.Nombre)
                    .FirstOrDefaultAsync() ?? $"#{usuarioId}";

                foreach (var adminId in admins)
                {
                    _context.Notificaciones.Add(new Notificacion
                    {
                        UsuarioId    = adminId,
                        Mensaje      = $"✅ Incidencia #{id} marcada como resuelta por {tecnicoNombreResolucion} — pendiente de cierre: {incidencia.Titulo}",
                        IncidenciaId = id
                    });
                    await _hub.Clients.Group($"user-{adminId}")
                        .SendAsync("NuevaNotificacion",
                            $"✅ Incidencia #{id} resuelta por {tecnicoNombreResolucion} — pendiente de cierre");
                }
            }

            // SignalR: avisar en tiempo real
            await _hub.Clients.Group("admins-tecnicos")
                .SendAsync("CambioEstado", id, dto.Estado);
            await _hub.Clients.Group($"user-{incidencia.UsuarioCreadorId}")
                .SendAsync("NuevaNotificacion", $"Incidencia #{id} → {dto.Estado}");
        }

        if (dto.TecnicoAsignadoId.HasValue && dto.TecnicoAsignadoId != incidencia.TecnicoAsignadoId)
        {
            var tecnicoNombre = await _context.Usuarios
                .Where(u => u.Id == dto.TecnicoAsignadoId.Value)
                .Select(u => u.Nombre)
                .FirstOrDefaultAsync() ?? $"#{dto.TecnicoAsignadoId}";

            await RegistrarAuditoria(id, usuarioId, "Técnico",
                incidencia.TecnicoAsignadoId.HasValue ? "Anterior técnico" : "Sin asignar",
                tecnicoNombre);

            incidencia.TecnicoAsignadoId = dto.TecnicoAsignadoId.Value;

            // Email al nuevo técnico asignado
            var tecnicoEmailActualizar = await _context.Usuarios
                .Where(u => u.Id == dto.TecnicoAsignadoId.Value)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(tecnicoEmailActualizar))
                await _email.EnviarAsignacionAsync(tecnicoEmailActualizar, tecnicoNombre,
                    id, incidencia.Titulo, incidencia.Descripcion,
                    incidencia.Prioridad, incidencia.Categoria);

            // SignalR: avisar al técnico asignado
            await _hub.Clients.Group($"user-{dto.TecnicoAsignadoId}")
                .SendAsync("NuevaNotificacion", $"Se te asignó la incidencia #{id}: {incidencia.Titulo}");
        }

        incidencia.FechaActualizacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(MapIncidencia(incidencia));
    }

    // DELETE: api/Incidencias/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia == null) return NotFound();
        _context.Incidencias.Remove(incidencia);
        await _context.SaveChangesAsync();
        return Ok("Incidencia eliminada.");
    }

    // GET: api/Incidencias/panel-tecnico
    [HttpGet("panel-tecnico")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> GetPanelTecnico(int pagina = 1, int tamanio = 10,
        string? estado = null, bool soloSinAsignar = false, bool soloAsignadas = false,
        string orden = "desc", string? categoria = null,
        string? prioridad = null, bool slaExcedido = false, string? q = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamanio < 1 || tamanio > 100) tamanio = 10;

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var query = _context.Incidencias
            .Include(i => i.UsuarioCreador)
            .Include(i => i.TecnicoAsignado)
            .Where(i => i.TecnicoAsignadoId == usuarioId || i.TecnicoAsignadoId == null)
            .AsQueryable();

        if (soloSinAsignar)
            query = query.Where(i => i.TecnicoAsignadoId == null);
        else if (soloAsignadas)
            query = query.Where(i => i.TecnicoAsignadoId == usuarioId);

        if (!string.IsNullOrEmpty(estado))
            query = query.Where(i => i.Estado == estado);
        if (!string.IsNullOrEmpty(categoria))
            query = query.Where(i => i.Categoria == categoria);
        if (!string.IsNullOrEmpty(prioridad))
            query = query.Where(i => i.Prioridad == prioridad);
        if (slaExcedido)
        {
            var ahora = DateTime.UtcNow;
            query = query.Where(i =>
                i.Estado != Estados.Resuelta && i.Estado != Estados.Cerrada && (
                    (i.Prioridad == "Critica" && i.FechaCreacion < ahora.AddHours(-2))  ||
                    (i.Prioridad == "Alta"    && i.FechaCreacion < ahora.AddHours(-8))  ||
                    (i.Prioridad == "Media"   && i.FechaCreacion < ahora.AddHours(-24)) ||
                    (i.Prioridad == "Baja"    && i.FechaCreacion < ahora.AddHours(-72))
                ));
        }
        query = AplicarFiltroQ(query, q);

        query = orden == "asc"
            ? query.OrderBy(i => i.FechaCreacion)
            : query.OrderByDescending(i => i.FechaCreacion);

        var total = await query.CountAsync();
        var incidencias = await query.Skip((pagina - 1) * tamanio).Take(tamanio).ToListAsync();

        return Ok(new
        {
            total,
            pagina,
            tamanio,
            totalPaginas = (int)Math.Ceiling((double)total / tamanio),
            datos = incidencias.Select(MapIncidencia)
        });
    }

    // GET: api/Incidencias/mis
    [HttpGet("mis")]
    public async Task<IActionResult> GetMias(int pagina = 1, int tamanio = 10,
        string? estado = null, string orden = "desc",
        string? categoria = null, string? prioridad = null, string? q = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamanio < 1 || tamanio > 50) tamanio = 10;

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var query = _context.Incidencias
            .Include(i => i.UsuarioCreador)
            .Include(i => i.TecnicoAsignado)
            .Where(i => i.UsuarioCreadorId == usuarioId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(estado))
            query = query.Where(i => i.Estado == estado);
        if (!string.IsNullOrEmpty(categoria))
            query = query.Where(i => i.Categoria == categoria);
        if (!string.IsNullOrEmpty(prioridad))
            query = query.Where(i => i.Prioridad == prioridad);
        query = AplicarFiltroQ(query, q);

        query = orden == "asc"
            ? query.OrderBy(i => i.FechaCreacion)
            : query.OrderByDescending(i => i.FechaCreacion);

        var total = await query.CountAsync();
        var incidencias = await query.Skip((pagina - 1) * tamanio).Take(tamanio).ToListAsync();

        return Ok(new
        {
            total,
            pagina,
            tamanio,
            totalPaginas = (int)Math.Ceiling((double)total / tamanio),
            datos = incidencias.Select(MapIncidencia)
        });
    }

    // GET: api/Incidencias/mis-stats
    [HttpGet("mis-stats")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> GetMisStats()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ahora = DateTime.UtcNow;

        var mis = await _context.Incidencias
            .Where(i => i.TecnicoAsignadoId == usuarioId)
            .ToListAsync();

        var activasAsignadas = mis.Count(i =>
            i.Estado == Estados.Abierta || i.Estado == Estados.EnProceso);

        var hace7dias = ahora.AddDays(-7);
        var resueltasEstaSemana = mis.Count(i =>
            (i.Estado == Estados.Resuelta || i.Estado == Estados.Cerrada) &&
            i.FechaActualizacion.HasValue && i.FechaActualizacion.Value >= hace7dias);

        var resueltas = mis
            .Where(i => (i.Estado == Estados.Resuelta || i.Estado == Estados.Cerrada)
                        && i.FechaActualizacion.HasValue)
            .ToList();

        double? tiempoMedioHoras = null;
        if (resueltas.Any())
        {
            var tiempos = resueltas
                .Select(i => (i.FechaActualizacion!.Value - i.FechaCreacion).TotalHours)
                .Where(h => h > 0).ToList();
            if (tiempos.Any())
                tiempoMedioHoras = Math.Round(tiempos.Average(), 1);
        }

        double? slaCumplidoPct = null;
        if (resueltas.Any())
        {
            var cumplidos = resueltas.Count(i =>
            {
                var limite = i.Prioridad switch
                {
                    "Critica" => 2.0, "Alta" => 8.0, "Media" => 24.0, _ => 72.0
                };
                return (i.FechaActualizacion!.Value - i.FechaCreacion).TotalHours <= limite;
            });
            slaCumplidoPct = Math.Round((double)cumplidos / resueltas.Count * 100, 1);
        }

        return Ok(new
        {
            activasAsignadas,
            resueltasEstaSemana,
            tiempoMedioHoras,
            slaCumplidoPct,
            totalResueltas = resueltas.Count
        });
    }

    // GET: api/Incidencias/consulta
    [HttpGet("consulta")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Consulta(string tipo, string? categoria = null)
    {
        var ahora = DateTime.UtcNow;

        string respuesta = tipo switch
        {
            "tecnico-mas-activas" => await TecnicoMasActivasAsync(),
            "tecnico-mas-resueltas" => await TecnicoMasResueltasAsync(null),
            "tecnico-mas-resueltas-categoria" => await TecnicoMasResueltasAsync(categoria),
            "categoria-mas-incidencias" => await CategoriaMasIncidenciasAsync(),
            "sla-excedido" => await SlaExcedidoAsync(ahora),
            "tiempo-medio" => await TiempoMedioAsync(),
            "resumen-estados" => await ResumenEstadosAsync(),
            "sin-asignar" => await SinAsignarAsync(),
            _ => "Consulta no reconocida."
        };

        return Ok(new { respuesta });
    }

    private async Task<string> TecnicoMasActivasAsync()
    {
        var grupo = await _context.Incidencias
            .Where(i => i.TecnicoAsignadoId != null &&
                        (i.Estado == Estados.Abierta || i.Estado == Estados.EnProceso))
            .GroupBy(i => i.TecnicoAsignadoId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .OrderByDescending(g => g.Total)
            .FirstOrDefaultAsync();

        if (grupo == null) return "No hay incidencias activas asignadas actualmente.";
        var nombre = await _context.Usuarios.Where(u => u.Id == grupo.Id).Select(u => u.Nombre).FirstOrDefaultAsync();
        return $"El técnico con más incidencias activas es **{nombre}** con {grupo.Total} incidencia{(grupo.Total == 1 ? "" : "s")}.";
    }

    private async Task<string> TecnicoMasResueltasAsync(string? categoria)
    {
        var query = _context.Incidencias
            .Where(i => i.TecnicoAsignadoId != null &&
                        (i.Estado == Estados.Resuelta || i.Estado == Estados.Cerrada));
        if (!string.IsNullOrEmpty(categoria))
            query = query.Where(i => i.Categoria == categoria);

        var grupo = await query
            .GroupBy(i => i.TecnicoAsignadoId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .OrderByDescending(g => g.Total)
            .FirstOrDefaultAsync();

        if (grupo == null)
            return categoria != null
                ? $"No hay incidencias resueltas en la categoría {categoria}."
                : "No hay incidencias resueltas aún.";

        var nombre = await _context.Usuarios.Where(u => u.Id == grupo.Id).Select(u => u.Nombre).FirstOrDefaultAsync();
        var sufijo = categoria != null ? $" en la categoría **{categoria}**" : "";
        return $"El técnico con más incidencias resueltas{sufijo} es **{nombre}** con {grupo.Total} incidencia{(grupo.Total == 1 ? "" : "s")}.";
    }

    private async Task<string> CategoriaMasIncidenciasAsync()
    {
        var grupo = await _context.Incidencias
            .Where(i => i.Categoria != null)
            .GroupBy(i => i.Categoria!)
            .Select(g => new { Categoria = g.Key, Total = g.Count() })
            .OrderByDescending(g => g.Total)
            .FirstOrDefaultAsync();

        if (grupo == null) return "No hay incidencias clasificadas aún.";
        return $"La categoría con más incidencias es **{grupo.Categoria}** con {grupo.Total} incidencia{(grupo.Total == 1 ? "" : "s")}.";
    }

    private async Task<string> SlaExcedidoAsync(DateTime ahora)
    {
        var abiertas = await _context.Incidencias
            .Where(i => i.Estado != Estados.Resuelta && i.Estado != Estados.Cerrada)
            .ToListAsync();

        var excedidas = abiertas.Count(i =>
        {
            var limite = i.Prioridad switch
            {
                "Critica" => 2.0, "Alta" => 8.0, "Media" => 24.0, _ => 72.0
            };
            return (ahora - i.FechaCreacion).TotalHours > limite;
        });

        if (excedidas == 0) return "Ninguna incidencia activa supera su SLA en este momento.";
        return $"Hay **{excedidas}** incidencia{(excedidas == 1 ? "" : "s")} con el SLA excedido de un total de {abiertas.Count} activas.";
    }

    private async Task<string> TiempoMedioAsync()
    {
        var resueltas = await _context.Incidencias
            .Where(i => (i.Estado == Estados.Resuelta || i.Estado == Estados.Cerrada)
                        && i.FechaActualizacion != null)
            .Select(i => new { i.FechaCreacion, Actualizada = i.FechaActualizacion!.Value })
            .ToListAsync();

        if (!resueltas.Any()) return "No hay incidencias resueltas aún para calcular el tiempo medio.";
        var tiempos = resueltas.Select(r => (r.Actualizada - r.FechaCreacion).TotalHours).Where(h => h > 0).ToList();
        if (!tiempos.Any()) return "No hay datos suficientes para calcular el tiempo medio.";
        var medio = Math.Round(tiempos.Average(), 1);
        return $"El tiempo medio de resolución global es **{medio} horas** ({Math.Round(medio / 24, 1)} días), calculado sobre {resueltas.Count} incidencias resueltas.";
    }

    private async Task<string> ResumenEstadosAsync()
    {
        var total     = await _context.Incidencias.CountAsync();
        var abiertas  = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Abierta);
        var enProceso = await _context.Incidencias.CountAsync(i => i.Estado == Estados.EnProceso);
        var resueltas = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Resuelta);
        var cerradas  = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Cerrada);

        return $"De un total de **{total}** incidencias: {abiertas} abiertas, {enProceso} en proceso, {resueltas} resueltas y {cerradas} cerradas.";
    }

    private async Task<string> SinAsignarAsync()
    {
        var total = await _context.Incidencias
            .CountAsync(i => i.TecnicoAsignadoId == null &&
                             (i.Estado == Estados.Abierta || i.Estado == Estados.EnProceso));
        if (total == 0) return "No hay incidencias activas sin técnico asignado en este momento.";
        return $"Hay **{total}** incidencia{(total == 1 ? "" : "s")} activa{(total == 1 ? "" : "s")} sin técnico asignado.";
    }

    // GET: api/Incidencias/stats
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetStats()
    {
        var total     = await _context.Incidencias.CountAsync();
        var abiertas  = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Abierta);
        var enProceso = await _context.Incidencias.CountAsync(i => i.Estado == Estados.EnProceso);
        var resueltas = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Resuelta);
        var cerradas  = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Cerrada);

        var porPrioridad = await _context.Incidencias
            .GroupBy(i => i.Prioridad)
            .Select(g => new { Prioridad = g.Key, Count = g.Count() })
            .ToListAsync();

        var resueltasData = await _context.Incidencias
            .Where(i => (i.Estado == Estados.Resuelta || i.Estado == Estados.Cerrada)
                        && i.FechaActualizacion != null)
            .Select(i => new { i.FechaCreacion, Actualizada = i.FechaActualizacion!.Value, i.Prioridad })
            .ToListAsync();

        double? tiempoMedioHoras = null;
        if (resueltasData.Count > 0)
        {
            var tiempos = resueltasData
                .Select(r => (r.Actualizada - r.FechaCreacion).TotalHours)
                .Where(h => h > 0).ToList();
            if (tiempos.Count > 0)
                tiempoMedioHoras = Math.Round(tiempos.Average(), 1);
        }

        var tiempoMedioPorPrioridad = new Dictionary<string, double?>();
        foreach (var prio in new[] { "Critica", "Alta", "Media", "Baja" })
        {
            var tiemposPrio = resueltasData
                .Where(r => r.Prioridad == prio)
                .Select(r => (r.Actualizada - r.FechaCreacion).TotalHours)
                .Where(h => h > 0).ToList();
            tiempoMedioPorPrioridad[prio] = tiemposPrio.Count > 0
                ? Math.Round(tiemposPrio.Average(), 1)
                : null;
        }

        var carga = await _context.Incidencias
            .Where(i => i.TecnicoAsignadoId != null &&
                        (i.Estado == Estados.Abierta || i.Estado == Estados.EnProceso))
            .GroupBy(i => i.TecnicoAsignadoId)
            .Select(g => new { TecnicoId = g.Key!.Value, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .FirstOrDefaultAsync();

        string? tecnicoMasCargaNombre = null;
        int?    tecnicoMasCargaCount  = null;
        if (carga != null)
        {
            tecnicoMasCargaCount = carga.Count;
            var tecnico = await _context.Usuarios.FindAsync(carga.TecnicoId);
            tecnicoMasCargaNombre = tecnico?.Nombre;
        }

        var hoy   = DateTime.UtcNow.Date;
        var hace6 = hoy.AddDays(-6);
        var fechas = await _context.Incidencias
            .Where(i => i.FechaCreacion >= hace6)
            .Select(i => i.FechaCreacion)
            .ToListAsync();

        var porDia = Enumerable.Range(0, 7)
            .Select(d => hoy.AddDays(-(6 - d)))
            .Select(fecha => new
            {
                etiqueta = fecha.ToString("dd/MM"),
                total    = fechas.Count(f => f.Date == fecha)
            }).ToList();

        var porCategoria = await _context.Incidencias
            .Where(i => i.Categoria != null)
            .GroupBy(i => i.Categoria!)
            .Select(g => new { categoria = g.Key, total = g.Count() })
            .OrderByDescending(g => g.total)
            .ToListAsync();

        var tecnicoGrupos = await _context.Incidencias
            .Where(i => i.TecnicoAsignadoId != null)
            .GroupBy(i => i.TecnicoAsignadoId!.Value)
            .Select(g => new { id = g.Key, total = g.Count() })
            .OrderByDescending(g => g.total)
            .ToListAsync();

        var tecnicoIds = tecnicoGrupos.Select(t => t.id).ToList();
        var tecnicosNombres = await _context.Usuarios
            .Where(u => tecnicoIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nombre })
            .ToListAsync();

        var porTecnico = tecnicoGrupos.Select(t => new
        {
            nombre = tecnicosNombres.FirstOrDefault(u => u.Id == t.id)?.Nombre ?? $"#{t.id}",
            total  = t.total
        }).ToList();

        var usuarioGrupos = await _context.Incidencias
            .GroupBy(i => i.UsuarioCreadorId)
            .Select(g => new { id = g.Key, total = g.Count() })
            .OrderByDescending(g => g.total)
            .Take(10)
            .ToListAsync();

        var usuarioIds = usuarioGrupos.Select(u => u.id).ToList();
        var usuariosNombres = await _context.Usuarios
            .Where(u => usuarioIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nombre })
            .ToListAsync();

        var porUsuario = usuarioGrupos.Select(u => new
        {
            nombre = usuariosNombres.FirstOrDefault(x => x.Id == u.id)?.Nombre ?? $"#{u.id}",
            total  = u.total
        }).ToList();

        return Ok(new
        {
            total,
            abiertas,
            enProceso,
            resueltas,
            cerradas,
            porPrioridad = porPrioridad.ToDictionary(x => x.Prioridad, x => x.Count),
            tiempoMedioHoras,
            tiempoMedioPorPrioridad,
            tecnicoMasCargaNombre,
            tecnicoMasCargaCount,
            porDia,
            porCategoria,
            porTecnico,
            porUsuario
        });
    }

    // PUT: api/Incidencias/{id}/asignar
    [HttpPut("{id}/asignar")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> Asignar(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia == null) return NotFound();

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        _context.HistorialEstados.Add(new HistorialEstado
        {
            EstadoAnterior = incidencia.Estado,
            EstadoNuevo    = Estados.EnProceso,
            UsuarioId      = usuarioId,
            IncidenciaId   = id
        });

        var tecnicoNombre = await _context.Usuarios
            .Where(u => u.Id == usuarioId)
            .Select(u => u.Nombre)
            .FirstOrDefaultAsync() ?? $"#{usuarioId}";

        await RegistrarAuditoria(id, usuarioId, "Técnico", "Sin asignar", tecnicoNombre);
        await RegistrarAuditoria(id, usuarioId, "Estado", incidencia.Estado, Estados.EnProceso);

        incidencia.TecnicoAsignadoId  = usuarioId;
        incidencia.Estado             = Estados.EnProceso;
        incidencia.FechaActualizacion = DateTime.UtcNow;

        _context.Notificaciones.Add(new Notificacion
        {
            UsuarioId    = usuarioId,
            Mensaje      = $"Se te asignó la incidencia #{id}: {incidencia.Titulo}",
            IncidenciaId = id
        });

        // Notificar a suscriptores
        await NotificarSuscriptoresAsync(id, usuarioId,
            $"[Seguimiento] Incidencia #{id} asignada a {tecnicoNombre}: {incidencia.Titulo}");

        await _context.SaveChangesAsync();

        // Email al técnico asignado
        var tecnicoEmail = await _context.Usuarios
            .Where(u => u.Id == usuarioId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(tecnicoEmail))
            await _email.EnviarAsignacionAsync(tecnicoEmail, tecnicoNombre,
                id, incidencia.Titulo, incidencia.Descripcion,
                incidencia.Prioridad, incidencia.Categoria);

        // SignalR: avisar en tiempo real
        await _hub.Clients.Group("admins-tecnicos")
            .SendAsync("CambioEstado", id, Estados.EnProceso);
        await _hub.Clients.Group($"user-{incidencia.UsuarioCreadorId}")
            .SendAsync("NuevaNotificacion", $"Incidencia #{id} ha sido asignada y está en proceso");

        return Ok(MapIncidencia(incidencia));
    }

    // POST: api/Incidencias/reclasificar
    [HttpPost("reclasificar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reclasificar()
    {
        var pendientes = await _context.Incidencias
            .Where(i => i.Categoria == null)
            .ToListAsync();

        if (pendientes.Count == 0)
            return Ok(new { actualizadas = 0, mensaje = "No hay incidencias sin categoría." });

        int actualizadas = 0;
        foreach (var inc in pendientes)
        {
            var clasificacion = await _clasificador.ClasificarAsync(inc.Titulo, inc.Descripcion);
            inc.Categoria       = clasificacion?.Categoria ?? "Otro";
            inc.JustificacionIA = clasificacion?.Justificacion;
            if (clasificacion?.Prioridad != null)
                inc.Prioridad = clasificacion.Prioridad;
            actualizadas++;
        }

        await _context.SaveChangesAsync();
        return Ok(new { actualizadas, mensaje = $"{actualizadas} incidencias clasificadas correctamente." });
    }

    // GET: api/Incidencias/{id}/suscrito
    [HttpGet("{id}/suscrito")]
    public async Task<IActionResult> GetSuscrito(int id)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var suscrito = await _context.Suscripciones
            .AnyAsync(s => s.IncidenciaId == id && s.UsuarioId == usuarioId);
        return Ok(new { suscrito });
    }

    // POST: api/Incidencias/{id}/suscribir
    [HttpPost("{id}/suscribir")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> Suscribir(int id)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await _context.Incidencias.AnyAsync(i => i.Id == id)) return NotFound();
        if (await _context.Suscripciones.AnyAsync(s => s.IncidenciaId == id && s.UsuarioId == usuarioId))
            return Ok(new { suscrito = true });

        _context.Suscripciones.Add(new SuscripcionIncidencia
        {
            UsuarioId    = usuarioId,
            IncidenciaId = id
        });
        await _context.SaveChangesAsync();
        return Ok(new { suscrito = true });
    }

    // DELETE: api/Incidencias/{id}/suscribir
    [HttpDelete("{id}/suscribir")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> Desuscribir(int id)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var s = await _context.Suscripciones
            .FirstOrDefaultAsync(s => s.IncidenciaId == id && s.UsuarioId == usuarioId);
        if (s == null) return NotFound();
        _context.Suscripciones.Remove(s);
        await _context.SaveChangesAsync();
        return Ok(new { suscrito = false });
    }

    // GET: api/Incidencias/{id}/sugerencia
    [HttpGet("{id}/sugerencia")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> Sugerencia(int id)
    {
        var inc = await _context.Incidencias.FindAsync(id);
        if (inc == null) return NotFound();

        var texto = await _clasificador.SugerirSolucionAsync(
            inc.Titulo, inc.Descripcion, inc.Categoria ?? "Otro");

        return Ok(new { sugerencia = texto ?? "No se pudo generar una sugerencia en este momento." });
    }

    // POST: api/Incidencias/consulta-libre
    [HttpPost("consulta-libre")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConsultaLibre([FromBody] Api.DTOs.ConsultaLibreDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Pregunta)) return BadRequest();

        var ahora = DateTime.UtcNow;

        // Totales globales
        var total     = await _context.Incidencias.CountAsync();
        var abiertas  = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Abierta);
        var enProceso = await _context.Incidencias.CountAsync(i => i.Estado == Estados.EnProceso);
        var resueltas = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Resuelta);
        var cerradas  = await _context.Incidencias.CountAsync(i => i.Estado == Estados.Cerrada);
        var sinAsignar = await _context.Incidencias.CountAsync(i =>
            i.TecnicoAsignadoId == null &&
            (i.Estado == Estados.Abierta || i.Estado == Estados.EnProceso));

        var todasActivas = await _context.Incidencias
            .Where(i => i.Estado != Estados.Resuelta && i.Estado != Estados.Cerrada)
            .ToListAsync();
        var slaExcedido = todasActivas.Count(i =>
        {
            var limite = i.Prioridad switch { "Critica" => 2.0, "Alta" => 8.0, "Media" => 24.0, _ => 72.0 };
            return (ahora - i.FechaCreacion).TotalHours > limite;
        });

        // Desglose por técnico: activas, resueltas y por categoría
        var tecnicos = await _context.Usuarios
            .Where(u => u.Rol == Roles.Tecnico)
            .ToListAsync();

        var todasIncidencias = await _context.Incidencias
            .Where(i => i.TecnicoAsignadoId != null)
            .ToListAsync();

        var tecnicoLineas = new List<string>();
        foreach (var tec in tecnicos)
        {
            var propias      = todasIncidencias.Where(i => i.TecnicoAsignadoId == tec.Id).ToList();
            if (propias.Count == 0) continue;
            var actTec       = propias.Count(i => i.Estado == Estados.Abierta || i.Estado == Estados.EnProceso);
            var resTec       = propias.Count(i => i.Estado == Estados.Resuelta || i.Estado == Estados.Cerrada);
            var catActivas   = propias.Where(i => i.Estado == Estados.Abierta || i.Estado == Estados.EnProceso)
                                      .GroupBy(i => i.Categoria ?? "Otro")
                                      .Select(g => $"{g.Key}:{g.Count()}")
                                      .ToList();
            var catResueltas = propias.Where(i => i.Estado == Estados.Resuelta || i.Estado == Estados.Cerrada)
                                      .GroupBy(i => i.Categoria ?? "Otro")
                                      .Select(g => $"{g.Key}:{g.Count()}")
                                      .ToList();
            tecnicoLineas.Add(
                $"{tec.Nombre} — abierta_o_en_proceso:{actTec} (cats:[{string.Join(", ", catActivas)}]), " +
                $"resuelta_o_cerrada:{resTec} (cats:[{string.Join(", ", catResueltas)}])");
        }

        // Desglose por categoría
        var categorias = await _context.Incidencias
            .Where(i => i.Categoria != null)
            .GroupBy(i => i.Categoria)
            .Select(g => new { Cat = g.Key, Total = g.Count() })
            .OrderByDescending(g => g.Total)
            .ToListAsync();

        // Tiempo medio de resolución global
        var incResueltas = await _context.Incidencias
            .Where(i => (i.Estado == Estados.Resuelta || i.Estado == Estados.Cerrada)
                        && i.FechaActualizacion != null)
            .Select(i => new { i.FechaCreacion, Actualizada = i.FechaActualizacion!.Value })
            .ToListAsync();
        var tiempoMedio = incResueltas.Count > 0
            ? Math.Round(incResueltas
                .Select(r => (r.Actualizada - r.FechaCreacion).TotalHours)
                .Where(h => h > 0).DefaultIfEmpty(0).Average(), 1)
            : (double?)null;

        var contexto = $"""
            RESUMEN GLOBAL:
            - Total incidencias: {total} ({abiertas} abiertas, {enProceso} en proceso, {resueltas} resueltas, {cerradas} cerradas)
            - Sin técnico asignado: {sinAsignar}
            - Con SLA excedido: {slaExcedido}
            - Tiempo medio de resolución: {(tiempoMedio.HasValue ? $"{tiempoMedio} horas ({Math.Round(tiempoMedio.Value / 24, 1)} días)" : "sin datos")}

            DESGLOSE POR TÉCNICO:
            {string.Join("\n", tecnicoLineas)}

            INCIDENCIAS POR CATEGORÍA:
            {string.Join(", ", categorias.Select(c => $"{c.Cat}: {c.Total}"))}
            """;

        var respuesta = await _clasificador.ConsultarLibreAsync(dto.Pregunta, contexto);
        return Ok(new { respuesta = respuesta ?? "No se pudo obtener respuesta en este momento." });
    }
}
