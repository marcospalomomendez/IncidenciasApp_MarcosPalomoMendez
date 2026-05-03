using System.Windows;

namespace Desktop;

public partial class MainWindow : Window
{
    public static string Token { get; set; } = string.Empty;
    public static string Rol { get; set; } = string.Empty;
    public static string Nombre { get; set; } = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        MainFrame.Navigate(new Views.LoginPage());
    }

    public void NavigateTo(Uri uri)
    {
        MainFrame.Navigate(uri);
    }
}