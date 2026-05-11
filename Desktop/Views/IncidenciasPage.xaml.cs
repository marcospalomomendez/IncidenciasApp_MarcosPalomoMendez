using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Desktop.Views;

public partial class IncidenciasPage : Page
{
    private readonly HttpClient _client;
    private List<IncidenciaItem> _todas = new();

    public IncidenciasPage()
    {
        InitializeComponent();
        _client = new HttpClient { BaseAddress = new Uri("http://localhost:5196") };
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MainWindow.Token);

        Loaded += async (s, e) =>
        {
            TxtNombre.Text = $"Hola, {MainWindow.Nombre} ({MainWindow.Rol})";
            await CargarIncidencias();
        };
    }

    private async Task CargarIncidencias()
    {
        try
        {
            var response = await _client.GetAsync("/api/Incidencias");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                _todas = JsonSerializer.Deserialize<List<IncidenciaItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                AplicarFiltro();
            }
        }
        catch
        {
            MessageBox.Show("Error al cargar las incidencias.", "Error");
        }
    }

    private void AplicarFiltro()
    {
        if (CmbFiltro == null || CmbFiltro.SelectedItem == null) return;
        if (DgIncidencias == null) return;

        var filtro = (CmbFiltro.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (filtro == "Todos")
            DgIncidencias.ItemsSource = _todas;
        else
            DgIncidencias.ItemsSource = _todas.Where(i => i.Estado == filtro).ToList();
    }

    private void CmbFiltro_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_todas == null) return;
        AplicarFiltro();
    }
    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await CargarIncidencias();
    }
    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Token = string.Empty;
        MainWindow.Rol = string.Empty;
        MainWindow.Nombre = string.Empty;
        var window = (MainWindow)Application.Current.MainWindow;
        window.MainFrame.Navigate(new LoginPage());
    }

    private void DgIncidencias_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgIncidencias.SelectedItem is IncidenciaItem incidencia)
        {
            var window = (MainWindow)Application.Current.MainWindow;
            window.MainFrame.Navigate(new DetallePage(incidencia.Id));
        }
    }
}

public class IncidenciaItem
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Prioridad { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public int? TecnicoAsignadoId { get; set; }
}