using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Web.Models;

namespace Web.Pages.Admin;

public class ExportarExcelModel : PageModel
{
    private readonly IHttpClientFactory _clientFactory;

    public ExportarExcelModel(IHttpClientFactory clientFactory)
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

        var incidencias = await FetchTodasAsync(client);

        // Filtro últimos 7 días
        var desde = DateTime.UtcNow.AddDays(-7);
        incidencias = incidencias.Where(i => i.FechaCreacion >= desde).ToList();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Incidencias");

        // Subtítulo del período
        var periodoCell = ws.Cell(1, 1);
        periodoCell.Value = $"Informe semanal: {desde:dd/MM/yyyy} — {DateTime.Now:dd/MM/yyyy}";
        ws.Range(1, 1, 1, 9).Merge();
        periodoCell.Style.Font.Bold = true;
        periodoCell.Style.Font.FontSize = 12;
        periodoCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3864");
        periodoCell.Style.Font.FontColor = XLColor.White;
        periodoCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Cabecera
        string[] headers = ["ID", "Título", "Descripción", "Estado", "Prioridad",
                             "Categoría", "Técnico Asignado (ID)", "Fecha Creación", "Fecha Actualización"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(2, c + 1);
            cell.Value = headers[c];
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E5090");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Font.Bold = true;
        }

        // Filas
        for (int r = 0; r < incidencias.Count; r++)
        {
            var i = incidencias[r];
            int row = r + 3;

            ws.Cell(row, 1).Value = i.Id;
            ws.Cell(row, 2).Value = i.Titulo;
            ws.Cell(row, 3).Value = i.Descripcion;
            ws.Cell(row, 4).Value = i.Estado;
            ws.Cell(row, 5).Value = i.Prioridad;
            ws.Cell(row, 6).Value = i.Categoria ?? "";
            ws.Cell(row, 7).Value = i.TecnicoAsignadoId.HasValue ? i.TecnicoAsignadoId.Value.ToString() : "";
            ws.Cell(row, 8).Value = i.FechaCreacion.ToString("dd/MM/yyyy HH:mm");
            ws.Cell(row, 9).Value = i.FechaActualizacion?.ToString("dd/MM/yyyy HH:mm") ?? "";

            if (r % 2 == 1)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        }

        ws.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fecha = DateTime.Now.ToString("yyyy-MM-dd");
        return File(stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"informe-semanal-{fecha}.xlsx");
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
            var pagina_datos = data.GetProperty("datos").Deserialize<List<IncidenciaModel>>(opts) ?? new();
            todas.AddRange(pagina_datos);
            pagina++;
        }
        while (pagina <= totalPaginas);

        return todas;
    }
}
