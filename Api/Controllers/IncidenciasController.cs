using Api.Data;
using Api.DTOs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public IncidenciasController(AppDbContext context, IClasificadorService clasificador)
    {
        _context = context;
        _clasificador = clasificador;
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

    // POST: api/Incidencias
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearIncidenciaDto dto)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var incidencia = new Incidencia
        {
            Titulo = dto.Titulo,
            Descripcion = dto.Descripcion,
            Prioridad = dto.Prioridad,
            UsuarioCreadorId = usuarioId
        };
        _context.Incidencias.Add(incidencia);
        await _context.SaveChangesAsync();

        var clasificacion = await _clasificador.ClasificarAsync(dto.Titulo, dto.Descripcion);
        incidencia.Categoria      = clasificacion?.Categoria ?? "Otro";
        incidencia.JustificacionIA = clasificacion?.Justificacion;
        if (clasificacion?.Prioridad != null)
            incidencia.Prioridad = clasificacion.Prioridad;
        await _context.SaveChangesAsync();

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

            // Notificar al creador del cambio de estado
            _context.Notificaciones.Add(new Notificacion
            {
                UsuarioId    = incidencia.UsuarioCreadorId,
                Mensaje      = $"Tu incidencia #{id} cambió de estado: {estadoAnterior} → {dto.Estado}",
                IncidenciaId = id
            });
        }

        if (dto.TecnicoAsignadoId.HasValue)
            incidencia.TecnicoAsignadoId = dto.TecnicoAsignadoId.Value;

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

        // Tiempo medio de resolución — client-side por limitación de SQLite
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

        // Tiempo medio por prioridad
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

        incidencia.TecnicoAsignadoId  = usuarioId;
        incidencia.Estado             = Estados.EnProceso;
        incidencia.FechaActualizacion = DateTime.UtcNow;

        // Notificar al técnico que se le asignó la incidencia
        _context.Notificaciones.Add(new Notificacion
        {
            UsuarioId    = usuarioId,
            Mensaje      = $"Se te asignó la incidencia #{id}: {incidencia.Titulo}",
            IncidenciaId = id
        });

        await _context.SaveChangesAsync();
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
            inc.Categoria      = clasificacion?.Categoria ?? "Otro";
            inc.JustificacionIA = clasificacion?.Justificacion;
            if (clasificacion?.Prioridad != null)
                inc.Prioridad = clasificacion.Prioridad;
            actualizadas++;
        }

        await _context.SaveChangesAsync();
        return Ok(new { actualizadas, mensaje = $"{actualizadas} incidencias clasificadas correctamente." });
    }
}
