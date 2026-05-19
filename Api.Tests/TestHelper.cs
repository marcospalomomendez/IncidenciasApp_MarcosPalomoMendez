using System.Net.Http.Json;
using System.Text.Json;
using Api.Data;
using Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public static class TestHelper
{
    public static async Task<string> RegistrarYLoginAsync(
        HttpClient client, string email,
        string password = "password123", string nombre = "Test User")
    {
        await client.PostAsync("/api/Auth/registro",
            JsonContent.Create(new { nombre, email, password }));

        var resp = await client.PostAsync("/api/Auth/login",
            JsonContent.Create(new { email, password }));

        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        return json.GetProperty("token").GetString()!;
    }

    public static async Task<string> CrearConRolYLoginAsync(
        IServiceProvider services, HttpClient client,
        string rol, string email, string password = "password123")
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Usuarios.Add(new Usuario
        {
            Nombre = $"Test {rol}",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Rol = rol
        });
        await db.SaveChangesAsync();

        var resp = await client.PostAsync("/api/Auth/login",
            JsonContent.Create(new { email, password }));
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        return json.GetProperty("token").GetString()!;
    }

    public static async Task<int> CrearIncidenciaAsync(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsync("/api/Incidencias",
            JsonContent.Create(new
            {
                titulo = "Incidencia de prueba",
                descripcion = "Descripcion de prueba con suficientes caracteres",
                prioridad = "Media"
            }));

        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        return json.GetProperty("id").GetInt32();
    }
}
