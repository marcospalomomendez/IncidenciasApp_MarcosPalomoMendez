using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Shared;

namespace Desktop.Views;

public partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        BtnLogin.IsEnabled = false;
        BtnLogin.Content = "Entrando...";

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                email = TxtEmail.Text,
                password = TxtPassword.Password
            });

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await MainWindow.ApiClient.PostAsync("/api/Auth/login", content);

            if (!response.IsSuccessStatusCode)
            {
                TxtError.Text = "Email o contraseña incorrectos.";
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);

            MainWindow.Token = result.GetProperty("token").GetString()!;
            MainWindow.Rol = result.GetProperty("rol").GetString()!;
            MainWindow.Nombre = result.GetProperty("nombre").GetString()!;
            MainWindow.UsuarioId = result.GetProperty("id").GetInt32();

            MainWindow.ApiClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", MainWindow.Token);

            if (MainWindow.Rol != Roles.Tecnico && MainWindow.Rol != Roles.Admin)
            {
                TxtError.Text = "Acceso solo para Técnicos y Administradores.";
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            var window = Application.Current.MainWindow as MainWindow;
            if (window != null)
            {
                window.MainFrame.Navigate(new IncidenciasPage());
            }
            else
            {
                TxtError.Text = "Error al navegar.";
                TxtError.Visibility = Visibility.Visible;
            }
        }
        catch (Exception)
        {
            TxtError.Text = "No se pudo conectar con el servidor. Comprueba que la API esté en marcha.";
            TxtError.Visibility = Visibility.Visible;
        }
        finally
        {
            BtnLogin.IsEnabled = true;
            BtnLogin.Content = "Iniciar sesión";
        }
    }
}