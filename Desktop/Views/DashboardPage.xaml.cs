using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Shared;

namespace Desktop.Views;

public partial class DashboardPage : Page
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions JsonCamel =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private bool _webViewReady = false;

    public DashboardPage()
    {
        InitializeComponent();
        TxtFecha.Text = DateTime.Now.ToString(
            "dddd, d 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"));

        Loaded += async (s, e) =>
        {
            var window = (MainWindow)Application.Current.MainWindow;
            window.SetActiveNav("dashboard");
            await InitWebViewAsync();
        };
    }

    // ── WebView2 init ────────────────────────────────────────────────
    private async Task InitWebViewAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(
                    Path.GetTempPath(), "IncidenciasApp_WebView2"));
            await WvDashboard.EnsureCoreWebView2Async(env);

            // Write template to temp file so CDN scripts load without CSP issues
            var tmpDir  = Path.Combine(Path.GetTempPath(), "IncidenciasApp");
            Directory.CreateDirectory(tmpDir);
            var tmpHtml = Path.Combine(tmpDir, "dashboard.html");
            await File.WriteAllTextAsync(tmpHtml, GetDashboardHtml());

            WvDashboard.NavigationCompleted += async (s, e) =>
            {
                if (_webViewReady || !e.IsSuccess) return;
                _webViewReady = true;
                await CargarDatos();
            };

            WvDashboard.Source = new Uri(tmpHtml);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al inicializar el dashboard: {ex.Message}\n\n" +
                "Asegúrate de tener instalado el WebView2 Runtime (viene con Microsoft Edge).",
                "Error");
        }
    }

    // ── Data loading ─────────────────────────────────────────────────
    private async Task CargarDatos()
    {
        try
        {
            var data = MainWindow.Rol == Roles.Admin
                ? await CargarDesdeStats()
                : await CargarDesdePanelTecnico();

            var json = JsonSerializer.Serialize(data, JsonCamel);
            await WvDashboard.ExecuteScriptAsync($"renderDashboard({json})");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar datos del dashboard: {ex.Message}", "Error");
        }
    }

    private async Task<DashboardData> CargarDesdeStats()
    {
        var res = await MainWindow.ApiClient.GetAsync("/api/Incidencias/stats");
        res.EnsureSuccessStatusCode();

        var json  = await res.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<StatsResponse>(json, JsonOpts)!;

        return new DashboardData
        {
            Total     = stats.Total,
            Abiertas  = stats.Abiertas,
            EnProceso = stats.EnProceso,
            Resueltas = stats.Resueltas,
            Cerradas  = stats.Cerradas,
            SlaExcedido = -1, // stats endpoint no lo devuelve; se oculta en JS
            PorPrioridad = stats.PorPrioridad,
            PorDia = stats.PorDia
                .Select(x => new DiaDato(x.Etiqueta, x.Total)).ToList(),
            PorCategoria = stats.PorCategoria
                .Select(x => new CategoriaDato(x.Categoria, x.Total)).ToList(),
            PorTecnico = stats.PorTecnico
                .Select(x => new NombreTotalDato(x.Nombre, x.Total)).ToList(),
            PorUsuario = stats.PorUsuario
                .Select(x => new NombreTotalDato(x.Nombre, x.Total)).ToList(),
            TiempoMedioPorPrioridad = stats.TiempoMedioPorPrioridad,
            TiempoMedioResolucion = stats.TiempoMedioHoras.HasValue
                ? $"{stats.TiempoMedioHoras.Value} horas" : "Sin datos",
            TecnicoMasCarga = stats.TecnicoMasCargaNombre != null
                ? $"{stats.TecnicoMasCargaNombre} ({stats.TecnicoMasCargaCount} incidencias)"
                : "Sin datos",
            EsAdmin = true
        };
    }

    private async Task<DashboardData> CargarDesdePanelTecnico()
    {
        var res = await MainWindow.ApiClient.GetAsync(
            "/api/Incidencias/panel-tecnico?pagina=1&tamanio=500");
        res.EnsureSuccessStatusCode();

        var json      = await res.Content.ReadAsStringAsync();
        var resultado = JsonSerializer.Deserialize<PaginadoWpf<IncidenciaItem>>(json, JsonOpts)!;
        var datos     = resultado.Datos;

        var hoy = DateTime.Today;
        var porDia = Enumerable.Range(0, 7)
            .Select(d => hoy.AddDays(-(6 - d)))
            .Select(fecha => new DiaDato(
                fecha.ToString("dd/MM"),
                datos.Count(i => i.FechaCreacion.ToLocalTime().Date == fecha)))
            .ToList();

        var porPrioridad = new[] { "Critica", "Alta", "Media", "Baja" }
            .ToDictionary(p => p, p => datos.Count(i => i.Prioridad == p));

        return new DashboardData
        {
            Total       = resultado.Total,
            Abiertas    = datos.Count(i => i.Estado == "Abierta"),
            EnProceso   = datos.Count(i => i.Estado == "EnProceso"),
            Resueltas   = datos.Count(i => i.Estado == "Resuelta"),
            Cerradas    = datos.Count(i => i.Estado == "Cerrada"),
            SlaExcedido = datos.Count(i => i.SlaExcedido),
            PorPrioridad = porPrioridad,
            PorDia       = porDia,
            EsAdmin      = false
        };
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_webViewReady) await CargarDatos();
    }

    // ── HTML template ────────────────────────────────────────────────
    private static string GetDashboardHtml() => """
        <!DOCTYPE html>
        <html lang="es">
        <head>
        <meta charset="utf-8">
        <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.4/dist/chart.umd.min.js"></script>
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;
             background:#F0F2F5;padding:16px;color:#374151}
        .kpi-row{display:grid;grid-template-columns:repeat(5,1fr);gap:12px;margin-bottom:14px}
        .kpi-row-4{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:14px}
        .kpi{background:white;border-radius:10px;padding:14px 16px;
             box-shadow:0 2px 8px rgba(0,0,0,.07)}
        .kpi-label{font-size:10px;font-weight:700;text-transform:uppercase;margin-bottom:8px}
        .kpi-value{font-size:30px;font-weight:700;line-height:1}
        .kpi-sub{font-size:11px;color:#9AA3B0;margin-top:4px}
        .row3{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-bottom:14px}
        .row2{display:grid;grid-template-columns:repeat(2,1fr);gap:12px;margin-bottom:14px}
        .card{background:white;border-radius:10px;padding:16px;
              box-shadow:0 2px 8px rgba(0,0,0,.07)}
        .card-title{font-size:13px;font-weight:700;color:#161D35;margin-bottom:14px}
        canvas{max-height:190px}
        table{width:100%;border-collapse:collapse;font-size:12px}
        th{text-align:left;padding:6px 8px;background:#F8FAFF;color:#6C757D;
           font-size:10px;font-weight:700;text-transform:uppercase}
        td{padding:7px 8px;border-top:1px solid #F0F2F5}
        .badge{display:inline-block;padding:2px 7px;border-radius:4px;
               font-size:11px;font-weight:600;color:white}
        p{margin-bottom:8px;font-size:13px}
        </style>
        </head>
        <body>
        <div id="kpiContainer"></div>
        <div class="row3">
          <div class="card"><div class="card-title">Incidencias por estado</div><canvas id="cEstados"></canvas></div>
          <div class="card"><div class="card-title">Distribución por prioridad</div><canvas id="cPrioridad"></canvas></div>
          <div class="card"><div class="card-title">Últimos 7 días</div><canvas id="cDias"></canvas></div>
        </div>
        <div class="row3" id="adminRow1" style="display:none">
          <div class="card"><div class="card-title">Por categoría</div><canvas id="cCategoria"></canvas></div>
          <div class="card"><div class="card-title">Por técnico</div><canvas id="cTecnico"></canvas></div>
          <div class="card"><div class="card-title">Por usuario</div><canvas id="cUsuario"></canvas></div>
        </div>
        <div class="row2" id="adminRow2" style="display:none">
          <div class="card">
            <div class="card-title">Tiempo medio de resolución por prioridad</div>
            <table>
              <thead><tr><th>Prioridad</th><th>SLA máx</th><th>Tiempo medio</th><th>Estado</th></tr></thead>
              <tbody id="slaBody"></tbody>
            </table>
          </div>
          <div class="card">
            <div class="card-title">Resumen general</div>
            <div id="resumen"></div>
          </div>
        </div>
        <script>
        let _charts = {};
        function destroyAll() { Object.values(_charts).forEach(c => c.destroy()); _charts = {}; }

        function renderDashboard(d) {
          destroyAll();

          // KPI cards
          const esAdmin = d.esAdmin;
          const kpis = esAdmin ? [
            {l:'TOTAL',      v:d.total,     s:'Incidencias', c:'#161D35'},
            {l:'ABIERTAS',   v:d.abiertas,  s:'Pendientes',  c:'#198754'},
            {l:'EN PROCESO', v:d.enProceso, s:'En trabajo',  c:'#D97706'},
            {l:'RESUELTAS',  v:d.resueltas, s:'Completadas', c:'#0D6EFD'},
            {l:'CERRADAS',   v:d.cerradas,  s:'Finalizadas', c:'#6C757D'}
          ] : [
            {l:'TOTAL',       v:d.total,       s:'Incidencias',    c:'#161D35'},
            {l:'ABIERTAS',    v:d.abiertas,    s:'Pendientes',     c:'#198754'},
            {l:'EN PROCESO',  v:d.enProceso,   s:'En trabajo',     c:'#D97706'},
            {l:'RESUELTAS',   v:d.resueltas,   s:'Completadas',    c:'#0D6EFD'},
            {l:'SLA ⚠',  v:d.slaExcedido, s:'Fuera de plazo', c:'#DC3545'}
          ];
          const cls = esAdmin ? 'kpi-row' : 'kpi-row';
          document.getElementById('kpiContainer').innerHTML =
            `<div class="${cls}">` +
            kpis.map(k => `<div class="kpi">
              <div class="kpi-label" style="color:${k.c}">${k.l}</div>
              <div class="kpi-value" style="color:${k.c}">${k.v}</div>
              <div class="kpi-sub">${k.s}</div>
            </div>`).join('') + '</div>';

          // Bar: estados
          _charts.estados = new Chart(document.getElementById('cEstados'), {
            type: 'bar',
            data: { labels: ['Abierta','En Proceso','Resuelta','Cerrada'],
              datasets: [{ data: [d.abiertas,d.enProceso,d.resueltas,d.cerradas],
                backgroundColor: ['#198754','#FFC107','#0DCAF0','#6C757D'] }] },
            options: { plugins:{legend:{display:false}}, scales:{y:{beginAtZero:true,ticks:{stepSize:1}}} }
          });

          // Donut: prioridad
          const pk = Object.keys(d.porPrioridad), pv = Object.values(d.porPrioridad);
          const pc = pk.map(k => ({Critica:'#DC3545',Alta:'#FFC107',Media:'#0D6EFD',Baja:'#ADB5BD'}[k]||'#ADB5BD'));
          _charts.prioridad = new Chart(document.getElementById('cPrioridad'), {
            type: 'doughnut',
            data: { labels: pk, datasets: [{ data: pv, backgroundColor: pc }] },
            options: { plugins: { legend: { position: 'bottom' } } }
          });

          // Line: 7 días
          _charts.dias = new Chart(document.getElementById('cDias'), {
            type: 'line',
            data: { labels: d.porDia.map(x => x.etiqueta),
              datasets: [{ label:'Creadas', data: d.porDia.map(x => x.total),
                borderColor:'#0D6EFD', backgroundColor:'rgba(13,110,253,.1)',
                fill:true, tension:0.3 }] },
            options: { scales: { y: { beginAtZero:true, ticks:{stepSize:1} } } }
          });

          // Admin-only charts
          if (esAdmin) {
            document.getElementById('adminRow1').style.display = 'grid';
            document.getElementById('adminRow2').style.display = 'grid';

            _charts.cat = new Chart(document.getElementById('cCategoria'), {
              type:'bar',
              data:{ labels:d.porCategoria.map(x=>x.categoria),
                datasets:[{data:d.porCategoria.map(x=>x.total),
                  backgroundColor:['#0DCAF0','#198754','#FFC107','#DC3545','#6C757D','#ADB5BD']}]},
              options:{indexAxis:'y',plugins:{legend:{display:false}},scales:{x:{beginAtZero:true}}}
            });
            _charts.tec = new Chart(document.getElementById('cTecnico'), {
              type:'bar',
              data:{ labels:d.porTecnico.map(x=>x.nombre),
                datasets:[{data:d.porTecnico.map(x=>x.total),backgroundColor:'#0D6EFD'}]},
              options:{indexAxis:'y',plugins:{legend:{display:false}},scales:{x:{beginAtZero:true}}}
            });
            _charts.usr = new Chart(document.getElementById('cUsuario'), {
              type:'bar',
              data:{ labels:d.porUsuario.map(x=>x.nombre),
                datasets:[{data:d.porUsuario.map(x=>x.total),backgroundColor:'#198754'}]},
              options:{indexAxis:'y',plugins:{legend:{display:false}},scales:{x:{beginAtZero:true}}}
            });

            // SLA table
            const slaRows = [
              {p:'Critica',m:2,  c:'#DC3545'},
              {p:'Alta',   m:8,  c:'#FFC107'},
              {p:'Media',  m:24, c:'#0D6EFD'},
              {p:'Baja',   m:72, c:'#ADB5BD'}
            ];
            document.getElementById('slaBody').innerHTML = slaRows.map(s => {
              const h = d.tiempoMedioPorPrioridad[s.p];
              const est = h != null
                ? (h <= s.m
                    ? '<span class="badge" style="background:#198754">✓ Dentro del SLA</span>'
                    : '<span class="badge" style="background:#DC3545">⚠ SLA excedido</span>')
                : '<span style="color:#9AA3B0">Sin datos</span>';
              return `<tr>
                <td><span class="badge" style="background:${s.c}">${s.p}</span></td>
                <td style="color:#9AA3B0">${s.m} h</td>
                <td><b>${h != null ? h + ' h' : '—'}</b></td>
                <td>${est}</td>
              </tr>`;
            }).join('');

            document.getElementById('resumen').innerHTML =
              `<p><b>Tiempo medio de resolución:</b> ${d.tiempoMedioResolucion||'Sin datos'}</p>
               <p><b>Técnico con más carga activa:</b> ${d.tecnicoMasCarga||'Sin datos'}</p>`;
          }
        }
        </script>
        </body></html>
        """;

    // ── DTOs ─────────────────────────────────────────────────────────
    private class DashboardData
    {
        public int    Total       { get; set; }
        public int    Abiertas    { get; set; }
        public int    EnProceso   { get; set; }
        public int    Resueltas   { get; set; }
        public int    Cerradas    { get; set; }
        public int    SlaExcedido { get; set; }
        public bool   EsAdmin     { get; set; }

        public Dictionary<string, int>     PorPrioridad            { get; set; } = new();
        public List<DiaDato>               PorDia                  { get; set; } = new();
        public List<CategoriaDato>         PorCategoria            { get; set; } = new();
        public List<NombreTotalDato>       PorTecnico              { get; set; } = new();
        public List<NombreTotalDato>       PorUsuario              { get; set; } = new();
        public Dictionary<string, double?> TiempoMedioPorPrioridad { get; set; } = new();
        public string TiempoMedioResolucion { get; set; } = "Sin datos";
        public string TecnicoMasCarga       { get; set; } = "Sin datos";
    }

    private record DiaDato(string Etiqueta, int Total);
    private record CategoriaDato(string Categoria, int Total);
    private record NombreTotalDato(string Nombre, int Total);

    private class StatsResponse
    {
        public int    Total     { get; set; }
        public int    Abiertas  { get; set; }
        public int    EnProceso { get; set; }
        public int    Resueltas { get; set; }
        public int    Cerradas  { get; set; }
        public Dictionary<string, int>     PorPrioridad            { get; set; } = new();
        public double?                     TiempoMedioHoras        { get; set; }
        public Dictionary<string, double?> TiempoMedioPorPrioridad { get; set; } = new();
        public string? TecnicoMasCargaNombre { get; set; }
        public int?    TecnicoMasCargaCount  { get; set; }
        public List<DiaDatoRaw>         PorDia       { get; set; } = new();
        public List<CategoriaDatoRaw>   PorCategoria { get; set; } = new();
        public List<NombreTotalDatoRaw> PorTecnico   { get; set; } = new();
        public List<NombreTotalDatoRaw> PorUsuario   { get; set; } = new();
    }

    private class DiaDatoRaw         { public string Etiqueta  { get; set; } = ""; public int Total { get; set; } }
    private class CategoriaDatoRaw   { public string Categoria { get; set; } = ""; public int Total { get; set; } }
    private class NombreTotalDatoRaw { public string Nombre    { get; set; } = ""; public int Total { get; set; } }
}