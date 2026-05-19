using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared;
using Xunit;

namespace Api.Tests;

public class IncidenciasTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public IncidenciasTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAll_SinToken_Devuelve401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/Incidencias");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetAll_ConToken_Devuelve200YPaginado()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "getall@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/Incidencias");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.True(json.TryGetProperty("total", out _));
        Assert.True(json.TryGetProperty("datos", out _));
    }

    [Fact]
    public async Task Crear_DatosValidos_Devuelve200()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "crear@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsync("/api/Incidencias",
            JsonContent.Create(new
            {
                titulo = "Incidencia de prueba",
                descripcion = "Descripcion de prueba con suficientes caracteres",
                prioridad = "Media"
            }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.True(json.TryGetProperty("id", out _));
        Assert.Equal("Abierta", json.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Crear_SinToken_Devuelve401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/Incidencias",
            JsonContent.Create(new { titulo = "Test", descripcion = "Test desc", prioridad = "Media" }));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetById_IncidenciaExistente_Devuelve200()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "getbyid@test.com");
        var id = await TestHelper.CrearIncidenciaAsync(client, token);

        var resp = await client.GetAsync($"/api/Incidencias/{id}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.Equal(id, json.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GetById_IncidenciaInexistente_Devuelve404()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "getbyid404@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/Incidencias/99999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Actualizar_EstadoInvalido_Devuelve400()
    {
        var client = _factory.CreateClient();
        var userToken = await TestHelper.RegistrarYLoginAsync(client, "est-inv-user@test.com");
        var id = await TestHelper.CrearIncidenciaAsync(client, userToken);

        var tecnicoToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Tecnico, "est-inv-tec@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tecnicoToken);

        var resp = await client.PutAsync($"/api/Incidencias/{id}",
            JsonContent.Create(new { estado = "EstadoQueNoExiste" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Actualizar_EstadoValido_ConRolTecnico_Devuelve200()
    {
        var client = _factory.CreateClient();
        var userToken = await TestHelper.RegistrarYLoginAsync(client, "act-valido-user@test.com");
        var id = await TestHelper.CrearIncidenciaAsync(client, userToken);

        var tecnicoToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Tecnico, "act-valido-tec@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tecnicoToken);

        var resp = await client.PutAsync($"/api/Incidencias/{id}",
            JsonContent.Create(new { estado = Estados.EnProceso }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Actualizar_ConRolUsuario_Devuelve403()
    {
        var client = _factory.CreateClient();
        var userToken = await TestHelper.RegistrarYLoginAsync(client, "act-usr-owner@test.com");
        var id = await TestHelper.CrearIncidenciaAsync(client, userToken);

        var otroToken = await TestHelper.RegistrarYLoginAsync(client, "act-usr-otro@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otroToken);

        var resp = await client.PutAsync($"/api/Incidencias/{id}",
            JsonContent.Create(new { estado = Estados.EnProceso }));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Eliminar_ConRolUsuario_Devuelve403()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "elim-usr@test.com");
        var id = await TestHelper.CrearIncidenciaAsync(client, token);

        var resp = await client.DeleteAsync($"/api/Incidencias/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Eliminar_ConRolAdmin_Devuelve200()
    {
        var client = _factory.CreateClient();
        var userToken = await TestHelper.RegistrarYLoginAsync(client, "elim-user@test.com");
        var id = await TestHelper.CrearIncidenciaAsync(client, userToken);

        var adminToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Admin, "elim-admin@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.DeleteAsync($"/api/Incidencias/{id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetMis_DevuelveSoloLasDelUsuario()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "mis@test.com");
        await TestHelper.CrearIncidenciaAsync(client, token);

        var resp = await client.GetAsync("/api/Incidencias/mis");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.True(json.GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public async Task Stats_ConRolAdmin_Devuelve200ConCamposEsperados()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Admin, "stats-admin@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync("/api/Incidencias/stats");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.True(json.TryGetProperty("total", out _));
        Assert.True(json.TryGetProperty("abiertas", out _));
        Assert.True(json.TryGetProperty("enProceso", out _));
        Assert.True(json.TryGetProperty("cerradas", out _));
        Assert.True(json.TryGetProperty("porPrioridad", out _));
    }

    [Fact]
    public async Task Stats_ConRolUsuario_Devuelve403()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "stats-usr@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/Incidencias/stats");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Asignar_ConRolTecnico_CambiaEstadoAEnProceso()
    {
        var client = _factory.CreateClient();
        var userToken = await TestHelper.RegistrarYLoginAsync(client, "asignar-usr@test.com");
        var id = await TestHelper.CrearIncidenciaAsync(client, userToken);

        var tecnicoToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Tecnico, "asignar-tec@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tecnicoToken);

        var resp = await client.PutAsync($"/api/Incidencias/{id}/asignar", null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.Equal(Estados.EnProceso, json.GetProperty("estado").GetString());
    }
}
