using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Producto_Terminado_Detalle")]
public class ProductoTerminadoDetalle
{
    [Key]
    [Column("id_articulo")]
    public int IdArticulo { get; set; }

    [MaxLength(10)]
    [Column("talla")]
    public string? Talla { get; set; }

    [MaxLength(30)]
    [Column("color")]
    public string? Color { get; set; }

    [ForeignKey(nameof(IdArticulo))]
    public Articulo Articulo { get; set; } = null!;
}