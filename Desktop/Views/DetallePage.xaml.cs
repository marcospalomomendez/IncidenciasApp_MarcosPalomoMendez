using System.Net.Http;
using System.Text;
using System.Text.Json;
using Shared;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Desktop.Views;

public partial class DetallePage : Page
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly int _incidenciaId;
    private bool _esSuscrito;

    public DetallePage(int id)
    {
        InitializeComponent();
        _incidenciaId = id;
        Loaded += async (s, e) => await CargarDetalle();
    }

    private async Task CargarDetalle()
    {
        try
        {
            var response = await MainWindow.ApiClient.GetAsync($"/api/Incidencias/{_incidenciaId}");
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var inc = JsonSerializer.Deserialize<JsonElement>(json);

            var titulo     = inc.GetProperty("titulo").GetString();
            var descripcion = inc.GetProperty("descripcion").GetString();
            var estado     = inc.GetProperty("estado").GetString();
            var prioridad  = inc.GetProperty("prioridad").GetString();
            var tecnicoId  = inc.GetProperty("tecnicoAsignadoId");
            var tecnicoTexto = tecnicoId.ValueKind == JsonValueKind.Null
                ? "Sin asignar" : "Asignada";
            var categoria = inc.TryGetProperty("categoria", out var catEl) && catEl.ValueKind != JsonValueKind.Null
                ? catEl.GetString() : null;
            var slaExcedido = inc.TryGetProperty("slaExcedido", out var slaEl) && slaEl.GetBoolean();

            var comentarios = JsonSerializer.Deserialize<List<ComentarioItem>>(
                inc.GetProperty("comentarios").GetRawText(), JsonOpts);

            Dispatcher.Invoke(() =>
            {
                TxtPageTitle.Text = $"#{_incidenciaId} — {titulo}";
                TxtTituloDetalle.Text = titulo;
                TxtDescripcion.Text = descripcion;
                TxtEstado.Text = estado;
                TxtPrioridad.Text = prioridad;
                TxtTecnico.Text = tecnicoTexto;

                BadgeEstado.Background = estado switch
                {
                    "Abierta"   => new SolidColorBrush(Color.FromRgb(25, 135, 84)),
                    "EnProceso" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                    "Resuelta"  => new SolidColorBrush(Color.FromRgb(13, 110, 253)),
                    "Cerrada"   => new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                    _           => new SolidColorBrush(Color.FromRgb(108, 117, 125))
                };
                TxtEstado.Foreground = estado == "EnProceso"
                    ? new SolidColorBrush(Colors.Black)
                    : new SolidColorBrush(Colors.White);

                BadgePrioridad.Background = prioridad switch
                {
                    "Critica" => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                    "Alta"    => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                    "Media"   => new SolidColorBrush(Color.FromRgb(13, 110, 253)),
                    _         => new SolidColorBrush(Color.FromRgb(108, 117, 125))
                };
                TxtPrioridad.Foreground = prioridad == "Alta"
                    ? new SolidColorBrush(Colors.Black)
                    : new SolidColorBrush(Colors.White);

                if (categoria != null)
                {
                    TxtCategoria.Text = categoria;
                    BadgeCategoria.Visibility = Visibility.Visible;
                }
                BadgeSla.Visibility = slaExcedido ? Visibility.Visible : Visibility.Collapsed;

                if (MainWindow.Rol == Roles.Admin)
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
            });

            if (MainWindow.Rol == Roles.Admin)
                await CargarTecnicos();

            await CargarAuditoria();
            await CargarSuscripcion();
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
            var response = await MainWindow.ApiClient.GetAsync("/api/Usuarios");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var todos = JsonSerializer.Deserialize<List<TecnicoItem>>(json, JsonOpts) ?? new();
                var tecnicos = todos.Where(u => u.Rol == Roles.Tecnico).ToList();
                Dispatcher.Invoke(() =>
                {
                    CmbTecnicos.ItemsSource = tecnicos;
                    CmbTecnicos.DisplayMemberPath = "Nombre";
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar técnicos: {ex.Message}");
        }
    }

    private async void BtnActualizarEstado_Click(object sender, RoutedEventArgs e)
    {
        var nuevoEstado = (CmbEstado.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(nuevoEstado)) return;

        var body = new StringContent(
            JsonSerializer.Serialize(new { estado = nuevoEstado }),
            Encoding.UTF8, "application/json");

        var response = await MainWindow.ApiClient.PutAsync($"/api/Incidencias/{_incidenciaId}", body);
        if (response.IsSuccessStatusCode)
        {
            MessageBox.Show("Estado actualizado correctamente.");
            await CargarDetalle();
        }
    }

    private async void BtnAsignar_Click(object sender, RoutedEventArgs e)
    {
        var response = await MainWindow.ApiClient.PutAsync($"/api/Incidencias/{_incidenciaId}/asignar", null);
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
            JsonSerializer.Serialize(new { tecnicoAsignadoId = tecnico.Id, estado = estadoActual }),
            Encoding.UTF8, "application/json");

        var response = await MainWindow.ApiClient.PutAsync($"/api/Incidencias/{_incidenciaId}", body);
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

        var response = await MainWindow.ApiClient.PostAsync("/api/Comentarios", body);
        if (response.IsSuccessStatusCode)
        {
            TxtComentario.Clear();
            await CargarDetalle();
        }
    }

    private async Task CargarAuditoria()
    {
        try
        {
            var response = await MainWindow.ApiClient.GetAsync($"/api/Incidencias/{_incidenciaId}/auditoria");
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var entries = JsonSerializer.Deserialize<List<AuditoriaItem>>(json, JsonOpts) ?? new();

            Dispatcher.Invoke(() => LstAuditoria.ItemsSource = entries);
        }
        catch { /* no crítico */ }
    }

    private async Task CargarSuscripcion()
    {
        try
        {
            var response = await MainWindow.ApiClient.GetAsync($"/api/Incidencias/{_incidenciaId}/suscrito");
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
            _esSuscrito = obj.GetProperty("suscrito").GetBoolean();

            Dispatcher.Invoke(ActualizarBtnSuscribir);
        }
        catch { /* no crítico */ }
    }

    private void ActualizarBtnSuscribir()
    {
        BtnSuscribir.Content = _esSuscrito ? "🔔 Cancelar seguimiento" : "🔔 Seguir incidencia";
        var bg = _esSuscrito ? "#F0F0F0" : "#EFF6FF";
        var fg = _esSuscrito ? "#6B7280" : "#1D4ED8";
        BtnSuscribir.Background = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bg));
        BtnSuscribir.Foreground = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fg));
    }

    private async void BtnSuscribir_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_esSuscrito)
                await MainWindow.ApiClient.DeleteAsync($"/api/Incidencias/{_incidenciaId}/suscribir");
            else
                await MainWindow.ApiClient.PostAsync($"/api/Incidencias/{_incidenciaId}/suscribir", null);

            await CargarSuscripcion();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cambiar suscripción: {ex.Message}");
        }
    }

    private void BtnVolver_Click(object sender, RoutedEventArgs e)
    {
        var window = (MainWindow)Application.Current.MainWindow;
        window.SetActiveNav("incidencias");
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

public class AuditoriaItem
{
    public int Id { get; set; }
    public string Campo { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }

    public string Descripcion => Campo switch
    {
        "Creación" => $"Creada",
        "Estado"   => $"{ValorAnterior} → {ValorNuevo}",
        "Técnico"  => $"Técnico: {ValorNuevo ?? "Sin asignar"}",
        _          => $"{Campo}: {ValorNuevo}"
    };

    public string MetaLinea => $"{UsuarioNombre} · {Fecha.ToLocalTime():dd/MM/yyyy HH:mm}";

    public string Icon => Campo switch
    {
        "Creación" => "+",
        "Estado"   => "↺",
        "Técnico"  => "T",
        _          => "•"
    };

    public System.Windows.Media.Brush IconColor => Campo switch
    {
        "Creación" => new System.Windows.Media.SolidColorBrush(
                          System.Windows.Media.Color.FromRgb(25, 135, 84)),
        "Estado"   => new System.Windows.Media.SolidColorBrush(
                          System.Windows.Media.Color.FromRgb(13, 110, 253)),
        "Técnico"  => new System.Windows.Media.SolidColorBrush(
                          System.Windows.Media.Color.FromRgb(217, 119, 6)),
        _          => new System.Windows.Media.SolidColorBrush(
                          System.Windows.Media.Color.FromRgb(108, 117, 125))
    };
}