using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Registro_DatosValidos_Devuelve200()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/Auth/registro",
            JsonContent.Create(new { nombre = "Usuario Test", email = "registro@test.com", password = "password123" }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Registro_EmailDuplicado_Devuelve400()
    {
        var client = _factory.CreateClient();
        var body = JsonContent.Create(new { nombre = "Dup", email = "dup@test.com", password = "password123" });
        await client.PostAsync("/api/Auth/registro", body);

        var resp = await client.PostAsync("/api/Auth/registro",
            JsonContent.Create(new { nombre = "Dup2", email = "dup@test.com", password = "password123" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Registro_PasswordCorta_Devuelve400()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/Auth/registro",
            JsonContent.Create(new { nombre = "Test", email = "short@test.com", password = "123" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Registro_EmailInvalido_Devuelve400()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/Auth/registro",
            JsonContent.Create(new { nombre = "Test", email = "noesunemail", password = "password123" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Login_CredencialesCorrectas_DevuelveToken()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/Auth/registro",
            JsonContent.Create(new { nombre = "Login OK", email = "loginok@test.com", password = "password123" }));

        var resp = await client.PostAsync("/api/Auth/login",
            JsonContent.Create(new { email = "loginok@test.com", password = "password123" }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.True(json.TryGetProperty("token", out var token));
        Assert.False(string.IsNullOrEmpty(token.GetString()));
    }

    [Fact]
    public async Task Login_PasswordIncorrecta_Devuelve401()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/Auth/registro",
            JsonContent.Create(new { nombre = "Wrong", email = "wrong@test.com", password = "password123" }));

        var resp = await client.PostAsync("/api/Auth/login",
            JsonContent.Create(new { email = "wrong@test.com", password = "incorrecta" }));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_EmailNoExiste_Devuelve401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/Auth/login",
            JsonContent.Create(new { email = "noexiste@test.com", password = "password123" }));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_Respuesta_ContieneRolYNombre()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/Auth/registro",
            JsonContent.Create(new { nombre = "Marcos Test", email = "campos@test.com", password = "password123" }));

        var resp = await client.PostAsync("/api/Auth/login",
            JsonContent.Create(new { email = "campos@test.com", password = "password123" }));

        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.True(json.TryGetProperty("rol", out _));
        Assert.True(json.TryGetProperty("nombre", out _));
        Assert.True(json.TryGetProperty("id", out _));
    }
}
