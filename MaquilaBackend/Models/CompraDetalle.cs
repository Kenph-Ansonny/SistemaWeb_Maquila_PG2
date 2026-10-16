using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("CompraDetalles")]
public class CompraDetalle
{
    [Key]
    [Column("id_detalle")]
    public long IdDetalle { get; set; }

    [Column("id_compra")]
    public int IdCompra { get; set; }

    [ForeignKey(nameof(IdCompra))]
    public Compra Compra { get; set; } = null!;

    [Column("id_articulo")]
    public int IdArticulo { get; set; }

    [ForeignKey(nameof(IdArticulo))]
    public Articulo Articulo { get; set; } = null!;

    [Column("id_unidad_medida")]
    public int IdUnidadMedida { get; set; }

    [ForeignKey(nameof(IdUnidadMedida))]
    public UnidadMedida UnidadMedida { get; set; } = null!;

    [Column("cantidad", TypeName = "decimal(12, 2)")]
    public decimal Cantidad { get; set; }

    [Column("costo_unitario", TypeName = "decimal(12, 4)")]
    public decimal CostoUnitario { get; set; }

    [Column("subtotal", TypeName = "decimal(14, 2)")]
    public decimal Subtotal { get; set; }

    [Column("id_almacen_destino")]
    public int IdAlmacenDestino { get; set; }

    [ForeignKey(nameof(IdAlmacenDestino))]
    public Almacen AlmacenDestino { get; set; } = null!;
}