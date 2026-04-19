using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using Web.Models;

namespace Web.Pages;

public class LoginModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public string Error { get; set; } = string.Empty;

    public LoginModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> OnPostAsync(string email, string password)
    {
        var client = _httpClientFactory.CreateClient("Api");
        var body = JsonSerializer.Serialize(new { email, password });
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/Auth/login", content);

        if (!response.IsSuccessStatusCode)
        {
            Error = "Email o contraseña incorrectos.";
            return Page();
        }

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<LoginResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        HttpContext.Session.SetString("Token", result!.Token);
        HttpContext.Session.SetString("Rol", result.Rol);
        HttpContext.Session.SetString("Nombre", result.Nombre);

        return RedirectToPage("/Incidencias/Index");
    }
}