using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Admin;

public class UsuariosModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public List<UsuarioModel> Usuarios { get; set; } = new();
    public string Mensaje { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;

    public UsuariosModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient GetClient()
    {
        var token = HttpContext.Session.GetString("Token");
        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!);
        return client;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var rol = HttpContext.Session.GetString("Rol");
        if (rol != "Admin")
            return RedirectToPage("/Index");

        var client = GetClient();
        var response = await client.GetAsync("/api/Usuarios");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            Usuarios = JsonSerializer.Deserialize<List<UsuarioModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        return Page();
    }

    // Cambiar rol de usuario existente
    public async Task<IActionResult> OnPostCambiarRolAsync(int id, string nuevoRol)
    {
        var client = GetClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { rol = nuevoRol }),
            Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/api/Usuarios/{id}/rol", body);
        if (response.IsSuccessStatusCode)
            Mensaje = "Rol actualizado correctamente.";
        else
            Error = "Error al actualizar el rol.";

        return await OnGetAsync();
    }
    public async Task<IActionResult> OnPostCrearAsync(string nombre, string email, string password, string rol)
    {
        var client = GetClient();

        // Registrar el usuario
        var body = new StringContent(
            JsonSerializer.Serialize(new { nombre, email, password }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/Auth/registro", body);

        if (!response.IsSuccessStatusCode)
        {
            Error = "Error al crear el usuario. El email puede estar ya registrado.";
            return await OnGetAsync();
        }

        // Si el rol no es Usuario, cambiarlo
        if (rol != "Usuario")
        {
            var resUsuarios = await client.GetAsync("/api/Usuarios");
            if (resUsuarios.IsSuccessStatusCode)
            {
                var json = await resUsuarios.Content.ReadAsStringAsync();
                var usuarios = JsonSerializer.Deserialize<List<UsuarioModel>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                var nuevoUsuario = usuarios.FirstOrDefault(u => u.Email == email);
                if (nuevoUsuario != null)
                {
                    var rolBody = new StringContent(
                        JsonSerializer.Serialize(new { rol }),
                        Encoding.UTF8, "application/json");
                    await client.PutAsync($"/api/Usuarios/{nuevoUsuario.Id}/rol", rolBody);
                }
            }
        }

        Mensaje = $"Usuario {nombre} creado correctamente con rol {rol}.";
        return await OnGetAsync();
    }
}