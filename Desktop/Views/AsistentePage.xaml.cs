using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Desktop.Views;

public partial class AsistentePage : Page
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, string> LabelPorTipo = new()
    {
        ["tecnico-mas-activas"]             = "¿Técnico con más incidencias activas?",
        ["tecnico-mas-resueltas"]           = "¿Técnico con más resueltas en total?",
        ["categoria-mas-incidencias"]       = "¿Categoría con más incidencias?",
        ["sin-asignar"]                     = "¿Incidencias sin asignar?",
        ["resumen-estados"]                 = "¿Resumen actual de estados?",
        ["sla-excedido"]                    = "¿SLA excedido?",
        ["tiempo-medio"]                    = "¿Tiempo medio de resolución?",
    };

    public AsistentePage()
    {
        InitializeComponent();
    }

    private async void BtnPreguntaFija_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var tipo = btn.Tag?.ToString() ?? "";
        var pregunta = LabelPorTipo.TryGetValue(tipo, out var lbl) ? lbl : tipo;

        AgregarBurbuja(pregunta, esUsuario: true);

        try
        {
            var url = $"/api/Incidencias/consulta?tipo={Uri.EscapeDataString(tipo)}";
            var res = await MainWindow.ApiClient.GetAsync(url);
            var respuesta = await ExtraerRespuesta(res);
            AgregarBurbuja(respuesta, esUsuario: false);
        }
        catch (Exception ex)
        {
            AgregarBurbuja($"Error: {ex.Message}", esUsuario: false);
        }
    }

    private async void BtnEnviarLibre_Click(object sender, RoutedEventArgs e)
    {
        await EnviarPreguntaLibre();
    }

    private async void TxtPreguntaLibre_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await EnviarPreguntaLibre();
    }

    private async Task EnviarPreguntaLibre()
    {
        var pregunta = TxtPreguntaLibre.Text.Trim();
        if (string.IsNullOrEmpty(pregunta)) return;

        TxtPreguntaLibre.Clear();
        AgregarBurbuja(pregunta, esUsuario: true);

        try
        {
            var body = new StringContent(
                JsonSerializer.Serialize(new { pregunta }),
                Encoding.UTF8, "application/json");
            var res = await MainWindow.ApiClient.PostAsync("/api/Incidencias/consulta-libre", body);
            var respuesta = await ExtraerRespuesta(res);
            AgregarBurbuja(respuesta, esUsuario: false);
        }
        catch (Exception ex)
        {
            AgregarBurbuja($"Error: {ex.Message}", esUsuario: false);
        }
    }

    private static async Task<string> ExtraerRespuesta(System.Net.Http.HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            return "No se pudo obtener respuesta del servidor.";

        var json = await res.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
        if (doc.TryGetProperty("respuesta", out var r))
            return r.GetString() ?? "Sin respuesta.";
        return "Sin respuesta.";
    }

    private void AgregarBurbuja(string texto, bool esUsuario)
    {
        TxtPlaceholder.Visibility = Visibility.Collapsed;

        if (esUsuario)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 110, 253)),
                CornerRadius = new CornerRadius(16, 16, 4, 16),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(60, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 420
            };
            border.Child = new TextBlock
            {
                Text = texto,
                Foreground = Brushes.White,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };
            PanelChat.Children.Add(border);
        }
        else
        {
            var row = new Grid { Margin = new Thickness(0, 0, 60, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatar = new Border
            {
                Width = 34, Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(Color.FromRgb(13, 110, 253))
            };
            avatar.Child = new TextBlock
            {
                Text = "IA", FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(avatar, 0);
            row.Children.Add(avatar);

            var bubble = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 242, 245)),
                CornerRadius = new CornerRadius(4, 16, 16, 16),
                Padding = new Thickness(12, 9, 12, 9)
            };
            bubble.Child = BuildRichText(texto);
            Grid.SetColumn(bubble, 2);
            row.Children.Add(bubble);

            PanelChat.Children.Add(row);
        }

        ScrollChat.ScrollToBottom();
    }

    private static TextBlock BuildRichText(string texto)
    {
        var tb = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        };

        // Parse **bold** markers
        var parts = Regex.Split(texto, @"\*\*(.+?)\*\*");
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            if (i % 2 == 1)
                tb.Inlines.Add(new Run(parts[i]) { FontWeight = FontWeights.Bold });
            else
                tb.Inlines.Add(new Run(parts[i]));
        }

        return tb;
    }

    private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
    {
        var toRemove = PanelChat.Children
            .OfType<UIElement>()
            .Where(c => c != TxtPlaceholder)
            .ToList();
        foreach (var child in toRemove)
            PanelChat.Children.Remove(child);

        TxtPlaceholder.Visibility = Visibility.Visible;
    }
}
