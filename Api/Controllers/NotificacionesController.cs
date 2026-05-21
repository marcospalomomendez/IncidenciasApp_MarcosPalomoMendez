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

    // PATCH: api/Notificaciones/{id}/leer
    [HttpPatch("{id}/leer")]
    public async Task<IActionResult> MarcarLeida(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var n = await _context.Notificaciones
            .FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == userId);
        if (n == null) return NotFound();
        n.Leida = true;
        await _context.SaveChangesAsync();
        return Ok();
    }

    // PATCH: api/Notificaciones/leer-todas
    [HttpPatch("leer-todas")]
    public async Task<IActionResult> MarcarTodasLeidas()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var pendientes = await _context.Notificaciones
            .Where(n => n.UsuarioId == userId && !n.Leida)
            .ToListAsync();
        pendientes.ForEach(n => n.Leida = true);
        await _context.SaveChangesAsync();
        return Ok(new { marcadas = pendientes.Count });
    }
}
