using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared;
using Xunit;

namespace Api.Tests;

public class ComentariosTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ComentariosTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CrearComentario_ConToken_Devuelve200()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "com-crear@test.com");
        var incId = await TestHelper.CrearIncidenciaAsync(client, token);

        var resp = await client.PostAsync("/api/Comentarios",
            JsonContent.Create(new { contenido = "Comentario de prueba", incidenciaId = incId }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CrearComentario_SinToken_Devuelve401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/Comentarios",
            JsonContent.Create(new { contenido = "Sin auth", incidenciaId = 1 }));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CrearComentario_IncidenciaInexistente_Devuelve404()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "com-404@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsync("/api/Comentarios",
            JsonContent.Create(new { contenido = "Comentario", incidenciaId = 99999 }));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetComentarios_DeIncidenciaConComentarios_DevuelveListaNoVacia()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "com-get@test.com");
        var incId = await TestHelper.CrearIncidenciaAsync(client, token);

        await client.PostAsync("/api/Comentarios",
            JsonContent.Create(new { contenido = "Primer comentario", incidenciaId = incId }));

        var resp = await client.GetAsync($"/api/Comentarios/{incId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.True(json.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetComentarios_IncidenciaSinComentarios_DevuelveListaVacia()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "com-empty@test.com");
        var incId = await TestHelper.CrearIncidenciaAsync(client, token);

        var resp = await client.GetAsync($"/api/Comentarios/{incId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.Equal(0, json.GetArrayLength());
    }

    [Fact]
    public async Task EliminarComentario_ConRolUsuario_Devuelve403()
    {
        var client = _factory.CreateClient();
        var token = await TestHelper.RegistrarYLoginAsync(client, "com-del-usr@test.com");
        var incId = await TestHelper.CrearIncidenciaAsync(client, token);

        var createResp = await client.PostAsync("/api/Comentarios",
            JsonContent.Create(new { contenido = "Comentario a eliminar", incidenciaId = incId }));
        var created = JsonSerializer.Deserialize<JsonElement>(await createResp.Content.ReadAsStringAsync());
        var comId = created.GetProperty("id").GetInt32();

        var resp = await client.DeleteAsync($"/api/Comentarios/{comId}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task EliminarComentario_ConRolAdmin_Devuelve200()
    {
        var client = _factory.CreateClient();
        var userToken = await TestHelper.RegistrarYLoginAsync(client, "com-del-user@test.com");
        var incId = await TestHelper.CrearIncidenciaAsync(client, userToken);

        var createResp = await client.PostAsync("/api/Comentarios",
            JsonContent.Create(new { contenido = "Comentario que borra el admin", incidenciaId = incId }));
        var created = JsonSerializer.Deserialize<JsonElement>(await createResp.Content.ReadAsStringAsync());
        var comId = created.GetProperty("id").GetInt32();

        var adminToken = await TestHelper.CrearConRolYLoginAsync(
            _factory.Services, client, Roles.Admin, "com-del-admin@test.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.DeleteAsync($"/api/Comentarios/{comId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
