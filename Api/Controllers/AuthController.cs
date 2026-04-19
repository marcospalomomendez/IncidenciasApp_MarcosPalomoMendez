using Api.Data;
using Api.Models;
using Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // POST: api/auth/registro
    [HttpPost("registro")]
    public async Task<IActionResult> Registro([FromBody] RegisterDto dto)
    {
        // Verificar que el email no esté registrado
        if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("El email ya está registrado.");

        // Crear el nuevo usuario
        var usuario = new Usuario
        {
            Nombre = dto.Nombre,
            Email = dto.Email,
            PasswordHash = HashPassword(dto.Password),
            Rol = "Usuario"
        };

        // Guardar el usuario en la base de datos
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        return Ok("Usuario registrado correctamente.");
    }

    // POST: api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // Buscar el usuario por email
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        // Verificar que el usuario exista y que la contraseña sea correcta
        if (usuario == null || !VerifyPassword(dto.Password, usuario.PasswordHash))
            return Unauthorized("Credenciales incorrectas.");

        // Generar el token JWT
        var token = GenerarToken(usuario);
        return Ok(new { token, rol = usuario.Rol, nombre = usuario.Nombre });
    }

    // Métodos auxiliares para hashing de contraseñas y generación de tokens
    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    private string GenerarToken(Usuario usuario)
    {
        // Crear los claims para el token
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Rol),
            new Claim(ClaimTypes.Name, usuario.Nombre)
        };

        // Crear la clave de seguridad y las credenciales de firma
        // La clave se lee desde appsettings.json para no exponerla en el código
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Crear el token JWT con expiración de 8 horas
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}