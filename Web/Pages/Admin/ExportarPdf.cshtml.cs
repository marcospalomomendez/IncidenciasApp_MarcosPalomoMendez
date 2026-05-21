using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Admin;

public class ExportarPdfModel : PageModel
{
    private readonly IHttpClientFactory _clientFactory;

    public ExportarPdfModel(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

        var client = _clientFactory.CreateClient("Api");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var todas = await FetchTodasAsync(client);

        // Filtro últimos 7 días
        var desde = DateTime.UtcNow.AddDays(-7);
        var incidencias = todas.Where(i => i.FechaCreacion >= desde).ToList();

        var porEstado    = incidencias.GroupBy(i => i.Estado).ToDictionary(g => g.Key, g => g.Count());
        var porPrioridad = incidencias.GroupBy(i => i.Prioridad).ToDictionary(g => g.Key, g => g.Count());
        var porCategoria = incidencias.Where(i => i.Categoria != null)
                                      .GroupBy(i => i.Categoria!)
                                      .ToDictionary(g => g.Key, g => g.Count());

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("Informe Semanal de Incidencias")
                        .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                    col.Item().Text($"Período: {desde:dd/MM/yyyy} — {DateTime.Now:dd/MM/yyyy}  |  Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    // Estadísticas
                    col.Item().Text("Resumen de la semana").FontSize(11).Bold();
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        // Total
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                        {
                            c.Item().Text("Total incidencias").Bold();
                            c.Item().Text(incidencias.Count.ToString())
                                .FontSize(18).Bold().FontColor(Colors.Blue.Medium);
                        });
                        row.ConstantItem(8);
                        // Por estado
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                        {
                            c.Item().Text("Por estado").Bold();
                            foreach (var kv in porEstado)
                                c.Item().Text($"{kv.Key}: {kv.Value}");
                        });
                        row.ConstantItem(8);
                        // Por prioridad
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                        {
                            c.Item().Text("Por prioridad").Bold();
                            foreach (var kv in porPrioridad)
                                c.Item().Text($"{kv.Key}: {kv.Value}");
                        });
                        row.ConstantItem(8);
                        // Por categoría
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                        {
                            c.Item().Text("Por categoría").Bold();
                            foreach (var kv in porCategoria)
                                c.Item().Text($"{kv.Key}: {kv.Value}");
                            if (!porCategoria.Any())
                                c.Item().Text("Sin clasificar").FontColor(Colors.Grey.Medium);
                        });
                    });

                    col.Item().PaddingTop(12).Text("Listado de incidencias de la semana").FontSize(11).Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);   // ID
                            cols.RelativeColumn(3);    // Título
                            cols.RelativeColumn(1.5f); // Estado
                            cols.RelativeColumn(1.5f); // Prioridad
                            cols.RelativeColumn(1.5f); // Categoría
                            cols.RelativeColumn(2);    // Fecha
                        });

                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(Colors.Blue.Darken3).Padding(4);

                        table.Header(h =>
                        {
                            foreach (var label in new[] { "ID", "Título", "Estado", "Prioridad", "Categoría", "Fecha" })
                                h.Cell().Element(HeaderCell).Text(label).FontColor(Colors.White).Bold();
                        });

                        bool par = false;
                        foreach (var i in incidencias)
                        {
                            par = !par;
                            IContainer RowCell(IContainer c) =>
                                c.Background(par ? Colors.White : Colors.Grey.Lighten3).Padding(3);

                            table.Cell().Element(RowCell).Text(i.Id.ToString());
                            table.Cell().Element(RowCell).Text(i.Titulo.Length > 50 ? i.Titulo[..50] + "…" : i.Titulo);
                            table.Cell().Element(RowCell).Text(i.Estado);
                            table.Cell().Element(RowCell).Text(i.Prioridad);
                            table.Cell().Element(RowCell).Text(i.Categoria ?? "—");
                            table.Cell().Element(RowCell).Text(i.FechaCreacion.ToString("dd/MM/yyyy"));
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        var fecha = DateTime.Now.ToString("yyyy-MM-dd");
        return File(bytes, "application/pdf", $"informe-semanal-{fecha}.pdf");
    }

    private async Task<List<IncidenciaModel>> FetchTodasAsync(HttpClient client)
    {
        var todas = new List<IncidenciaModel>();
        int pagina = 1;
        int totalPaginas;
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        do
        {
            var resp = await client.GetAsync($"api/Incidencias?pagina={pagina}&tamanio=1000");
            if (!resp.IsSuccessStatusCode) break;

            var data = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
            totalPaginas = data.GetProperty("totalPaginas").GetInt32();
            var datos = data.GetProperty("datos").Deserialize<List<IncidenciaModel>>(opts) ?? new();
            todas.AddRange(datos);
            pagina++;
        }
        while (pagina <= totalPaginas);

        return todas;
    }
}
