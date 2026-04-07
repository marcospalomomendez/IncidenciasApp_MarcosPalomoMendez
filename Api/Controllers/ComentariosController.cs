using Api.Data;
using Api.DTOs;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers;

// Controlador para gestionar los comentarios de las incidencias
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComentariosController : ControllerBase
{
    private readonly AppDbContext _context;

    public ComentariosController(AppDbContext context)
    {
        _context = context;
    }
    // GET: api/comentarios/{incidenciaId}
    [HttpGet("{incidenciaId}")]
    public async Task<IActionResult> GetByIncidencia(int incidenciaId)
    {
        // Obtener los comentarios de una incidencia específica, incluyendo el nombre del usuario que hizo cada comentario
        var comentarios = await _context.Comentarios
            .Include(c => c.Usuario)
            .Where(c => c.IncidenciaId == incidenciaId)
            .ToListAsync();
        return Ok(comentarios);
    }
    // POST: api/comentarios
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ComentarioDto dto)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // Verificar que la incidencia existe
        var incidencia = await _context.Incidencias.FindAsync(dto.IncidenciaId);
        if (incidencia == null) return NotFound("Incidencia no encontrada.");
        // Crear el comentario
        var comentario = new Comentario
        {
            Contenido = dto.Contenido,
            IncidenciaId = dto.IncidenciaId,
            UsuarioId = usuarioId
        };
        // Solo el creador de la incidencia o un administrador pueden agregar comentarios
        _context.Comentarios.Add(comentario);
        await _context.SaveChangesAsync();
        return Ok(comentario);
    }
    // DELETE: api/comentarios/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        // Solo los administradores pueden eliminar comentarios
        var comentario = await _context.Comentarios.FindAsync(id);
        if (comentario == null) return NotFound();

        _context.Comentarios.Remove(comentario);
        await _context.SaveChangesAsync();
        return Ok("Comentario eliminado.");
    }
}