using Microsoft.AspNetCore.SignalR.Client;
using Shared;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Desktop;

public partial class MainWindow : Window
{
    public static string Token { get; set; } = string.Empty;
    public static string Rol { get; set; } = string.Empty;
    public static string Nombre { get; set; } = string.Empty;
    public static int UsuarioId { get; set; } = 0;

    public static readonly HttpClient ApiClient = new();

    private HubConnection? _hub;
    private DispatcherTimer? _toastTimer;

    public MainWindow()
    {
        InitializeComponent();
        ApiClient.BaseAddress = new Uri(App.ApiUrl);
        ShowSidebar(false);
        MainFrame.Navigate(new Views.LoginPage());
    }

    public void ShowSidebar(bool show)
    {
        Sidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SidebarCol.Width = show ? new GridLength(250) : new GridLength(0);
    }

    public void UpdateSidebarUser()
    {
        TxtSidebarNombre.Text = Nombre.Length > 18 ? Nombre[..15] + "…" : Nombre;
        TxtSidebarRol.Text = Rol;
        TxtAvatar.Text = Nombre.Length > 0 ? Nombre[0].ToString().ToUpper() : "U";
        BtnNavDashboard.Visibility  = Rol == Roles.Admin ? Visibility.Visible : Visibility.Collapsed;
        BtnNavUsuarios.Visibility   = Rol == Roles.Admin ? Visibility.Visible : Visibility.Collapsed;
        BtnNavAsistente.Visibility  = Rol == Roles.Admin ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetActiveNav(string page)
    {
        BtnNavDashboard.Style  = page == "dashboard"  ? (Style)FindResource("NavBtnActive") : (Style)FindResource("NavBtn");
        BtnNavIncidencias.Style = page == "incidencias" ? (Style)FindResource("NavBtnActive") : (Style)FindResource("NavBtn");
        BtnNavUsuarios.Style   = page == "usuarios"   ? (Style)FindResource("NavBtnActive") : (Style)FindResource("NavBtn");
        BtnNavAsistente.Style  = page == "asistente"  ? (Style)FindResource("NavBtnActive") : (Style)FindResource("NavBtn");
    }

    // ── SignalR ───────────────────────────────────────────────────────────
    public async Task ConnectSignalRAsync()
    {
        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(App.ApiUrl.TrimEnd('/') + "/hubs/incidencias", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(Token);
                })
                .WithAutomaticReconnect()
                .Build();

            _hub.On<int, string, string?>("NuevaIncidencia", (id, titulo, cat) =>
                Dispatcher.Invoke(() =>
                    MostrarToast($"Nueva incidencia #{id}: {titulo}")));

            _hub.On<int, string>("CambioEstado", (id, estado) =>
                Dispatcher.Invoke(() =>
                    MostrarToast($"Incidencia #{id} → {estado}")));

            _hub.On<string>("NuevaNotificacion", msg =>
                Dispatcher.Invoke(() => MostrarToast(msg)));

            await _hub.StartAsync();
        }
        catch { /* SignalR no crítico: continuar sin tiempo real */ }
    }

    public async Task DisconnectSignalRAsync()
    {
        if (_hub != null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    public void MostrarToast(string mensaje)
    {
        TxtToastMsg.Text = mensaje;
        ToastBar.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _toastTimer.Tick += (s, e) =>
        {
            ToastBar.Visibility = Visibility.Collapsed;
            _toastTimer!.Stop();
        };
        _toastTimer.Start();
    }

    // ── Navigation ────────────────────────────────────────────────────────
    private void BtnNavDashboard_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav("dashboard");
        MainFrame.Navigate(new Views.DashboardPage());
    }

    private void BtnNavIncidencias_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav("incidencias");
        MainFrame.Navigate(new Views.IncidenciasPage());
    }

    private void BtnNavUsuarios_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav("usuarios");
        MainFrame.Navigate(new Views.UsuariosPage());
    }

    private void BtnNavAsistente_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav("asistente");
        MainFrame.Navigate(new Views.AsistentePage());
    }

    private async void BtnSidebarLogout_Click(object sender, RoutedEventArgs e)
    {
        await DisconnectSignalRAsync();
        Token = string.Empty;
        Rol = string.Empty;
        Nombre = string.Empty;
        ApiClient.DefaultRequestHeaders.Authorization = null;
        ShowSidebar(false);
        MainFrame.Navigate(new Views.LoginPage());
    }

    public void NavigateTo(Uri uri)
    {
        MainFrame.Navigate(uri);
    }
}
