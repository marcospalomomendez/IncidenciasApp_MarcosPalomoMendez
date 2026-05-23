using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Desktop.Views;

public partial class UsuariosPage : Page
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public UsuariosPage()
    {
        InitializeComponent();
        Loaded += async (s, e) => await CargarUsuarios();
    }

    private async Task CargarUsuarios()
    {
        try
        {
            var response = await MainWindow.ApiClient.GetAsync("/api/Usuarios");
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var usuarios = JsonSerializer.Deserialize<List<UsuarioItem>>(json, JsonOpts) ?? new();

            Dispatcher.Invoke(() => GridUsuarios.ItemsSource = usuarios);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar usuarios: {ex.Message}");
        }
    }

    private async void BtnGuardarRol_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var userId = (int)btn.Tag;

        var panel = btn.Parent as StackPanel;
        if (panel == null) return;

        var cmb = panel.Children.OfType<ComboBox>().FirstOrDefault();
        if (cmb == null) return;

        var nuevoRol = (cmb.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(nuevoRol)) return;

        try
        {
            var body = new StringContent(
                JsonSerializer.Serialize(new { rol = nuevoRol }),
                Encoding.UTF8, "application/json");

            var response = await MainWindow.ApiClient.PutAsync($"/api/Usuarios/{userId}/rol", body);
            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Rol actualizado correctamente.");
                await CargarUsuarios();
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                MessageBox.Show($"Error: {err}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al actualizar el rol: {ex.Message}");
        }
    }
}

public class UsuarioItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;

    public System.Windows.Media.Brush RolColor => Rol switch
    {
        "Admin"   => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
        "Tecnico" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 110, 253)),
        _         => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125))
    };
}
