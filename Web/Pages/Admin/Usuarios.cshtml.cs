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

    public UsuariosModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var rol = HttpContext.Session.GetString("Rol");
        if (rol != "Admin")
            return RedirectToPage("/Index");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Usuarios");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            Usuarios = JsonSerializer.Deserialize<List<UsuarioModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string nuevoRol)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        var client = _httpClientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var body = new StringContent(
            JsonSerializer.Serialize(new { rol = nuevoRol }),
            Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/api/Usuarios/{id}/rol", body);

        if (response.IsSuccessStatusCode)
            Mensaje = "Rol actualizado correctamente.";

        return await OnGetAsync();
    }
}