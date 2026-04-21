using Api.Data;
using Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsuariosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var usuarios = await _context.Usuarios
            .Select(u => new { u.Id, u.Nombre, u.Email, u.Rol })
            .ToListAsync();
        return Ok(usuarios);
    }

    [HttpPut("{id}/rol")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CambiarRol(int id, [FromBody] CambiarRolDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        if (dto.Rol != "Usuario" && dto.Rol != "Tecnico" && dto.Rol != "Admin")
            return BadRequest("Rol no válido.");

        usuario.Rol = dto.Rol;
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Rol actualizado.", usuario.Id, usuario.Nombre, usuario.Rol });
    }
}
