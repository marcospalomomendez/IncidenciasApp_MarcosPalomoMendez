using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Desktop.Views;

public partial class LoginPage : Page
{
    private HttpClient _client;

    public LoginPage()
    {
        InitializeComponent();
        _client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5196")
        };
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
            var response = await _client.PostAsync("/api/Auth/login", content);

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

            if (MainWindow.Rol != "Tecnico" && MainWindow.Rol != "Admin")
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
        catch (Exception ex)
        {
            TxtError.Text = ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
            TxtError.Visibility = Visibility.Visible;
        }
        finally
        {
            BtnLogin.IsEnabled = true;
            BtnLogin.Content = "Iniciar sesión";
        }
    }
}