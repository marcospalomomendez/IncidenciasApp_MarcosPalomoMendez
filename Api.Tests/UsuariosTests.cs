using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Data;
using Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Xunit;

namespace Api.Tests;

public class UsuariosTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UsuariosTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetUsuarios_ConRolAdmin_Devuelve200()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Admin, "admin-get@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync("/api/Usuarios");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetUsuarios_ConRolUsuario_Devuelve403()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "usr-get@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/Usuarios");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetUsuarios_SinToken_Devuelve401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/Usuarios");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CambiarRol_ConRolAdmin_Devuelve200()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Admin, "admin-cambiar@test.com");

        // Crear usuario objetivo
        int tecnicoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tecnico = new Usuario
            {
                Nombre = "Tecnico Target",
                Email = "tecnico-target@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Rol = Roles.Tecnico
            };
            db.Usuarios.Add(tecnico);
            await db.SaveChangesAsync();
            tecnicoId = tecnico.Id;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resp = await client.PutAsync($"/api/Usuarios/{tecnicoId}/rol",
            JsonContent.Create(new { rol = Roles.Usuario }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CambiarRol_ConRolUsuario_Devuelve403()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "usr-cambiar@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PutAsync("/api/Usuarios/1/rol",
            JsonContent.Create(new { rol = Roles.Admin }));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}

// Clase separada para aislar el test del único admin (BD propia)
public class UltimoAdminTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UltimoAdminTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CambiarRol_UnicoAdmin_Devuelve400()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Admin, "unico-admin@test.com");

        // Obtener el ID del admin creado
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var usersResp = await client.GetAsync("/api/Usuarios");
        var users = JsonSerializer.Deserialize<JsonElement>(await usersResp.Content.ReadAsStringAsync());

        int adminId = 0;
        foreach (var u in users.EnumerateArray())
        {
            if (u.GetProperty("email").GetString() == "unico-admin@test.com")
            {
                adminId = u.GetProperty("id").GetInt32();
                break;
            }
        }

        var resp = await client.PutAsync($"/api/Usuarios/{adminId}/rol",
            JsonContent.Create(new { rol = Roles.Tecnico }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
