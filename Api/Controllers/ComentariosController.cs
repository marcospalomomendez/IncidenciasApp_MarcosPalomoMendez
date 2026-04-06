using Api.Data;
using Api.DTOs;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers;

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

    [HttpGet("{incidenciaId}")]
    public async Task<IActionResult> GetByIncidencia(int incidenciaId)
    {
        var comentarios = await _context.Comentarios
            .Include(c => c.Usuario)
            .Where(c => c.IncidenciaId == incidenciaId)
            .ToListAsync();
        return Ok(comentarios);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ComentarioDto dto)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var incidencia = await _context.Incidencias.FindAsync(dto.IncidenciaId);
        if (incidencia == null) return NotFound("Incidencia no encontrada.");

        var comentario = new Comentario
        {
            Contenido = dto.Contenido,
            IncidenciaId = dto.IncidenciaId,
            UsuarioId = usuarioId
        };

        _context.Comentarios.Add(comentario);
        await _context.SaveChangesAsync();
        return Ok(comentario);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var comentario = await _context.Comentarios.FindAsync(id);
        if (comentario == null) return NotFound();

        _context.Comentarios.Remove(comentario);
        await _context.SaveChangesAsync();
        return Ok("Comentario eliminado.");
    }
}