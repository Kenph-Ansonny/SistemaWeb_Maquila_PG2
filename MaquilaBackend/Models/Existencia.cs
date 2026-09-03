using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Existencias")]
public class Existencia
{
    [Column("id_articulo")]
    public int IdArticulo { get; set; }

    [Column("id_almacen")]
    public int IdAlmacen { get; set; }

    [Column("stock_actual")]
    public decimal StockActual { get; set; } = 0.0000m;

    [ForeignKey(nameof(IdArticulo))]
    public Articulo Articulo { get; set; } = null!;

    [ForeignKey(nameof(IdAlmacen))]
    public Almacen Almacen { get; set; } = null!;
}