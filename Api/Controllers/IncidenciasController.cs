using Api.Data;
using Api.DTOs;
using Api.Models;
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

    public IncidenciasController(AppDbContext context)
    {
        _context = context;
    }
    // GET: api/Incidencias
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var incidencias = await _context.Incidencias
            .Include(i => i.UsuarioCreador)
            .Include(i => i.TecnicoAsignado)
            .ToListAsync();
        return Ok(incidencias);
    }
    // GET: api/Incidencias/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        // Carga la incidencia junto con su creador, técnico asignado, comentarios e historial de estados
        var incidencia = await _context.Incidencias
            .Include(i => i.UsuarioCreador)
            .Include(i => i.TecnicoAsignado)
            .Include(i => i.Comentarios)
            .Include(i => i.Historial)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (incidencia == null) return NotFound();

        // Devolvemos un DTO plano para evitar referencias circulares
        return Ok(new
        {
            incidencia.Id,
            incidencia.Titulo,
            incidencia.Descripcion,
            incidencia.Estado,
            incidencia.Prioridad,
            incidencia.FechaCreacion,
            incidencia.FechaActualizacion,
            incidencia.UsuarioCreadorId,
            incidencia.TecnicoAsignadoId,
            Comentarios = incidencia.Comentarios.Select(c => new
            {
                c.Id,
                c.Contenido,
                c.FechaCreacion,
                c.UsuarioId,
                c.IncidenciaId
            }),
            Historial = incidencia.Historial.Select(h => new
            {
                h.Id,
                h.EstadoAnterior,
                h.EstadoNuevo,
                h.FechaCambio,
                h.UsuarioId,
                h.IncidenciaId
            })
        });
    }
    // POST: api/Incidencias
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearIncidenciaDto dto)
    {
        // Obtiene el ID del usuario autenticado desde el token JWT
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // Crea una nueva incidencia con los datos proporcionados y el ID del usuario creador
        var incidencia = new Incidencia
        {
            Titulo = dto.Titulo,
            Descripcion = dto.Descripcion,
            Prioridad = dto.Prioridad,
            UsuarioCreadorId = usuarioId
        };
        // Agrega la incidencia a la base de datos y guarda los cambios
        _context.Incidencias.Add(incidencia);
        await _context.SaveChangesAsync();
        return Ok(incidencia);
    }
    // PUT: api/Incidencias/{id}
    [HttpPut("{id}")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarIncidenciaDto dto)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia == null) return NotFound();

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Solo registrar historial si el estado cambia
        if (!string.IsNullOrEmpty(dto.Estado) && dto.Estado != incidencia.Estado)
        {
            var historial = new HistorialEstado
            {
                EstadoAnterior = incidencia.Estado,
                EstadoNuevo = dto.Estado,
                UsuarioId = usuarioId,
                IncidenciaId = id
            };
            incidencia.Estado = dto.Estado;
            _context.HistorialEstados.Add(historial);
        }

        // Actualizar técnico SOLO si viene en el DTO (no null)
        if (dto.TecnicoAsignadoId.HasValue)
            incidencia.TecnicoAsignadoId = dto.TecnicoAsignadoId.Value;

        incidencia.FechaActualizacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(incidencia);
    }
    // DELETE: api/Incidencias/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        // Busca la incidencia por su ID en la base de datos
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia == null) return NotFound();
        // Elimina la incidencia de la base de datos y guarda los cambios
        _context.Incidencias.Remove(incidencia);
        await _context.SaveChangesAsync();
        return Ok("Incidencia eliminada.");
    }
    // GET: api/Incidencias/mis
    [HttpGet("mis")]
    public async Task<IActionResult> GetMias()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var incidencias = await _context.Incidencias
            .Include(i => i.UsuarioCreador)
            .Include(i => i.TecnicoAsignado)
            .Where(i => i.UsuarioCreadorId == usuarioId)
            .OrderByDescending(i => i.FechaCreacion)
            .ToListAsync();

        return Ok(incidencias);
    }
    // PUT: api/Incidencias/{id}/asignar
    [HttpPut("{id}/asignar")]
    [Authorize(Roles = "Tecnico,Admin")]
    public async Task<IActionResult> Asignar(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia == null) return NotFound();

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Registrar cambio en historial
        var historial = new HistorialEstado
        {
            EstadoAnterior = incidencia.Estado,
            EstadoNuevo = Estados.EnProceso,
            UsuarioId = usuarioId,
            IncidenciaId = id
        };

        incidencia.TecnicoAsignadoId = usuarioId;
        incidencia.Estado = Estados.EnProceso;
        incidencia.FechaActualizacion = DateTime.UtcNow;

        _context.HistorialEstados.Add(historial);
        await _context.SaveChangesAsync();

        return Ok(incidencia);
    }
}