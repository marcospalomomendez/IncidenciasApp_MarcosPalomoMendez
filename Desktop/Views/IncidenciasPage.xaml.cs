using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shared;

namespace Desktop.Views;

public partial class IncidenciasPage : Page
{
    private List<IncidenciaItem> _todas = new();
    private int _paginaActual = 1;
    private int _totalPaginas = 1;
    private int _total = 0;
    private const int Tamanio = 10;

    public IncidenciasPage()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            var window = (MainWindow)Application.Current.MainWindow;
            window.SetActiveNav("incidencias");

            if (MainWindow.Rol == Roles.Tecnico)
            {
                BtnMisIncidencias.Visibility = Visibility.Visible;
                BtnSinAsignar.Visibility = Visibility.Visible;
            }
            await CargarIncidencias();
        };
    }

    private string _modo = "todas";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private async Task CargarIncidencias()
    {
        try
        {
            string url;
            if (MainWindow.Rol == Roles.Tecnico)
            {
                url = _modo switch
                {
                    "mias"       => $"/api/Incidencias/panel-tecnico?pagina={_paginaActual}&tamanio={Tamanio}&soloAsignadas=true",
                    "sinAsignar" => $"/api/Incidencias/panel-tecnico?pagina={_paginaActual}&tamanio={Tamanio}&soloSinAsignar=true",
                    _            => $"/api/Incidencias/panel-tecnico?pagina={_paginaActual}&tamanio={Tamanio}"
                };
            }
            else
            {
                url = $"/api/Incidencias?pagina={_paginaActual}&tamanio={Tamanio}";
            }

            var response = await MainWindow.ApiClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var resultado = JsonSerializer.Deserialize<PaginadoWpf<IncidenciaItem>>(json, JsonOpts);

                _todas = resultado?.Datos ?? new();
                _totalPaginas = resultado?.TotalPaginas ?? 1;
                _total = resultado?.Total ?? _todas.Count;

                Dispatcher.Invoke(() =>
                {
                    AplicarFiltro();
                    ActualizarPaginacion();
                    TxtTotal.Text = $"{_total} incidencias en total";
                });
            }
        }
        catch
        {
            MessageBox.Show("Error al cargar las incidencias.", "Error");
        }
    }

    private void ActualizarBotonesModo()
    {
        BtnMisIncidencias.Style = _modo == "mias"
            ? (Style)Application.Current.FindResource("PrimaryBtn")
            : (Style)Application.Current.FindResource("SecondaryBtn");
        BtnSinAsignar.Style = _modo == "sinAsignar"
            ? (Style)Application.Current.FindResource("PrimaryBtn")
            : (Style)Application.Current.FindResource("SecondaryBtn");

        // Restore small padding after style change
        BtnMisIncidencias.Padding = new Thickness(10, 5, 10, 5);
        BtnSinAsignar.Padding = new Thickness(10, 5, 10, 5);
    }

    private async void BtnMisIncidencias_Click(object sender, RoutedEventArgs e)
    {
        _modo = _modo == "mias" ? "todas" : "mias";
        _paginaActual = 1;
        ActualizarBotonesModo();
        await CargarIncidencias();
    }

    private async void BtnSinAsignar_Click(object sender, RoutedEventArgs e)
    {
        _modo = _modo == "sinAsignar" ? "todas" : "sinAsignar";
        _paginaActual = 1;
        ActualizarBotonesModo();
        await CargarIncidencias();
    }

    private void AplicarFiltro()
    {
        if (CmbFiltro == null || CmbFiltro.SelectedItem == null) return;
        if (DgIncidencias == null) return;
        var filtro = (CmbFiltro.SelectedItem as ComboBoxItem)?.Content?.ToString();
        DgIncidencias.ItemsSource = filtro == "Todos"
            ? _todas
            : _todas.Where(i => i.Estado == filtro).ToList();
    }

    private void ActualizarPaginacion()
    {
        TxtPagina.Text = $"Pág {_paginaActual} / {_totalPaginas}";
        BtnAnterior.IsEnabled = _paginaActual > 1;
        BtnSiguiente.IsEnabled = _paginaActual < _totalPaginas;
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

    private async void BtnAnterior_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual > 1)
        {
            _paginaActual--;
            await CargarIncidencias();
        }
    }

    private async void BtnSiguiente_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual < _totalPaginas)
        {
            _paginaActual++;
            await CargarIncidencias();
        }
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
    public string? Categoria { get; set; }
    public bool SlaExcedido { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int? TecnicoAsignadoId { get; set; }
    public string TecnicoNombre => TecnicoAsignadoId.HasValue ? "Asignada" : "Sin asignar";
}

public class PaginadoWpf<T>
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int Tamanio { get; set; }
    public int TotalPaginas { get; set; }
    public List<T> Datos { get; set; } = new();
}