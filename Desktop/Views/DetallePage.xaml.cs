using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Desktop.Views;

public partial class DetallePage : Page
{
    private readonly HttpClient _client;
    private readonly int _incidenciaId;

    public DetallePage(int id)
    {
        InitializeComponent();
        _incidenciaId = id;
        _client = new HttpClient { BaseAddress = new Uri("http://localhost:5196") };
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MainWindow.Token);
        Loaded += async (s, e) => await CargarDetalle();
    }

    private async Task CargarDetalle()
    {
        try
        {
            var response = await _client.GetAsync($"/api/Incidencias/{_incidenciaId}");
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var inc = JsonSerializer.Deserialize<JsonElement>(json);

            var titulo = inc.GetProperty("titulo").GetString();
            var descripcion = inc.GetProperty("descripcion").GetString();
            var estado = inc.GetProperty("estado").GetString();
            var prioridad = inc.GetProperty("prioridad").GetString();
            var tecnicoId = inc.GetProperty("tecnicoAsignadoId");
            var tecnicoTexto = tecnicoId.ValueKind == JsonValueKind.Null
                ? "Sin asignar" : $"Técnico #{tecnicoId.GetInt32()}";

            var comentarios = JsonSerializer.Deserialize<List<ComentarioItem>>(
                inc.GetProperty("comentarios").GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var historial = JsonSerializer.Deserialize<List<HistorialItem>>(
                inc.GetProperty("historial").GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Dispatcher.Invoke(() =>
            {
                TxtTitulo.Text = titulo;
                TxtDescripcion.Text = descripcion;
                TxtEstado.Text = estado;
                TxtPrioridad.Text = prioridad;
                TxtTecnico.Text = tecnicoTexto;

                if (MainWindow.Rol == "Admin")
                {
                    BtnAsignar.Visibility = Visibility.Collapsed;
                    TxtLabelTecnicos.Visibility = Visibility.Visible;
                    CmbTecnicos.Visibility = Visibility.Visible;
                    BtnAsignarTecnico.Visibility = Visibility.Visible;
                }
                else
                {
                    BtnAsignar.IsEnabled = tecnicoId.ValueKind == JsonValueKind.Null;
                    TxtLabelTecnicos.Visibility = Visibility.Collapsed;
                    CmbTecnicos.Visibility = Visibility.Collapsed;
                    BtnAsignarTecnico.Visibility = Visibility.Collapsed;
                }

                foreach (ComboBoxItem item in CmbEstado.Items)
                {
                    if (item.Content.ToString() == estado)
                    {
                        CmbEstado.SelectedItem = item;
                        break;
                    }
                }

                LstComentarios.ItemsSource = comentarios;
                LstHistorial.ItemsSource = historial;
            });

            if (MainWindow.Rol == "Admin")
                await CargarTecnicos();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar el detalle: {ex.Message}");
        }
    }
    private async Task CargarTecnicos()
    {
        try
        {
            await Task.Delay(100);
            var response = await _client.GetAsync("/api/Usuarios");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var todos = JsonSerializer.Deserialize<List<TecnicoItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                var tecnicos = todos.Where(u => u.Rol == "Tecnico").ToList();

                MessageBox.Show($"Técnicos encontrados: {tecnicos.Count}");

                Dispatcher.Invoke(() =>
                {
                    CmbTecnicos.ItemsSource = tecnicos;
                    CmbTecnicos.DisplayMemberPath = "Nombre";
                });
            }
            else
            {
                MessageBox.Show($"Error al cargar usuarios: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excepción: {ex.Message}");
        }
    }

    private async void BtnActualizarEstado_Click(object sender, RoutedEventArgs e)
    {
        var nuevoEstado = (CmbEstado.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(nuevoEstado)) return;

        var body = new StringContent(
            JsonSerializer.Serialize(new { estado = nuevoEstado }),
            Encoding.UTF8, "application/json");

        var response = await _client.PutAsync($"/api/Incidencias/{_incidenciaId}", body);
        if (response.IsSuccessStatusCode)
        {
            MessageBox.Show("Estado actualizado correctamente.");
            await CargarDetalle();
        }
    }

    private async void BtnAsignar_Click(object sender, RoutedEventArgs e)
    {
        var response = await _client.PutAsync($"/api/Incidencias/{_incidenciaId}/asignar", null);
        if (response.IsSuccessStatusCode)
        {
            MessageBox.Show("Incidencia asignada correctamente.");
            await CargarDetalle();
        }
    }

    private async void BtnAsignarTecnico_Click(object sender, RoutedEventArgs e)
    {
        if (CmbTecnicos.SelectedItem is not TecnicoItem tecnico)
        {
            MessageBox.Show("Selecciona un técnico.");
            return;
        }

        var estadoActual = (CmbEstado.SelectedItem as ComboBoxItem)?.Content?.ToString();

        var body = new StringContent(
            JsonSerializer.Serialize(new
            {
                tecnicoAsignadoId = tecnico.Id,
                estado = estadoActual
            }),
            Encoding.UTF8, "application/json");

        var response = await _client.PutAsync($"/api/Incidencias/{_incidenciaId}", body);
        if (response.IsSuccessStatusCode)
        {
            MessageBox.Show($"Incidencia asignada a {tecnico.Nombre}.");
            await CargarDetalle();
        }
    }

    private async void BtnComentario_Click(object sender, RoutedEventArgs e)
    {
        var contenido = TxtComentario.Text.Trim();
        if (string.IsNullOrEmpty(contenido)) return;

        var body = new StringContent(
            JsonSerializer.Serialize(new { contenido, incidenciaId = _incidenciaId }),
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/Comentarios", body);
        if (response.IsSuccessStatusCode)
        {
            TxtComentario.Clear();
            await CargarDetalle();
        }
    }

    private void BtnVolver_Click(object sender, RoutedEventArgs e)
    {
        var window = (MainWindow)Application.Current.MainWindow;
        window.MainFrame.Navigate(new IncidenciasPage());
    }
}

public class TecnicoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}

public class ComentarioItem
{
    public int Id { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public int UsuarioId { get; set; }
}

public class HistorialItem
{
    public int Id { get; set; }
    public string EstadoAnterior { get; set; } = string.Empty;
    public string EstadoNuevo { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; }
}