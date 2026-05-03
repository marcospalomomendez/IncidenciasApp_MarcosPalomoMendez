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

            TxtTitulo.Text = inc.GetProperty("titulo").GetString();
            TxtDescripcion.Text = inc.GetProperty("descripcion").GetString();
            TxtEstado.Text = inc.GetProperty("estado").GetString();
            TxtPrioridad.Text = inc.GetProperty("prioridad").GetString();

            var tecnicoId = inc.GetProperty("tecnicoAsignadoId");
            TxtTecnico.Text = tecnicoId.ValueKind == JsonValueKind.Null
                ? "Sin asignar" : $"Técnico #{tecnicoId.GetInt32()}";

            BtnAsignar.IsEnabled = tecnicoId.ValueKind == JsonValueKind.Null;

            // Seleccionar estado actual en ComboBox
            foreach (ComboBoxItem item in CmbEstado.Items)
            {
                if (item.Content.ToString() == TxtEstado.Text)
                {
                    CmbEstado.SelectedItem = item;
                    break;
                }
            }

            // Comentarios
            var comentarios = inc.GetProperty("comentarios");
            LstComentarios.ItemsSource = JsonSerializer.Deserialize<List<ComentarioItem>>(
                comentarios.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Historial
            var historial = inc.GetProperty("historial");
            LstHistorial.ItemsSource = JsonSerializer.Deserialize<List<HistorialItem>>(
                historial.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar el detalle: {ex.Message}");
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