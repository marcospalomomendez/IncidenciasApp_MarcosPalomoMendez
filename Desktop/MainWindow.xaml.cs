using System.Net.Http;
using System.Windows;

namespace Desktop;

public partial class MainWindow : Window
{
    public static string Token { get; set; } = string.Empty;
    public static string Rol { get; set; } = string.Empty;
    public static string Nombre { get; set; } = string.Empty;
    public static int UsuarioId { get; set; } = 0;

    public static readonly HttpClient ApiClient = new();

    public MainWindow()
    {
        InitializeComponent();
        ApiClient.BaseAddress = new Uri(App.ApiUrl);
        MainFrame.Navigate(new Views.LoginPage());
    }

    public void NavigateTo(Uri uri)
    {
        MainFrame.Navigate(uri);
    }
}