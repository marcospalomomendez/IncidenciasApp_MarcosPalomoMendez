using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificacionesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Notificaciones
    [HttpGet]
    public async Task<IActionResult> GetMias()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var notifs = await _context.Notificaciones
            .Where(n => n.UsuarioId == userId)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(30)
            .Select(n => new { n.Id, n.Mensaje, n.Leida, n.FechaCreacion, n.IncidenciaId })
            .ToListAsync();

        return Ok(new
        {
            noLeidas = notifs.Count(n => !n.Leida),
            notificaciones = notifs
        });
    }

    // DELETE: api/Notificaciones/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var n = await _context.Notificaciones
            .FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == userId);
        if (n == null) return NotFound();
        _context.Notificaciones.Remove(n);
        await _context.SaveChangesAsync();
        return Ok();
    }

    // DELETE: api/Notificaciones/todas
    [HttpDelete("todas")]
    public async Task<IActionResult> EliminarTodas()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var todas = await _context.Notificaciones
            .Where(n => n.UsuarioId == userId)
            .ToListAsync();
        _context.Notificaciones.RemoveRange(todas);
        await _context.SaveChangesAsync();
        return Ok(new { eliminadas = todas.Count });
    }
}
