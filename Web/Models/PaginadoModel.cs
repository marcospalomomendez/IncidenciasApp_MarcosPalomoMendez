namespace Web.Models;

public class PaginadoModel<T>
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int Tamanio { get; set; }
    public int TotalPaginas { get; set; }
    public List<T> Datos { get; set; } = new();
}