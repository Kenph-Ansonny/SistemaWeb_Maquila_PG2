using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Kardex_Movimientos")]
public class KardexMovimiento
{
    [Key]
    [Column("id_kardex")]
    public long IdKardex { get; set; }

    [Column("id_articulo")]
    public int IdArticulo { get; set; }

    [ForeignKey(nameof(IdArticulo))]
    public Articulo Articulo { get; set; } = null!;

    [Column("id_almacen")]
    public int IdAlmacen { get; set; }

    [ForeignKey(nameof(IdAlmacen))]
    public Almacen Almacen { get; set; } = null!;

    // ENUM('ENTRADA_COMPRA', 'CONSUMO_PRODUCCION', 'ENTRADA_PRODUCIDO', 'MERMA_CORTE', 'SALIDA_PEDIDO', 'AJUSTE_INVENTARIO')
    [Required]
    [MaxLength(30)]
    [Column("tipo_movimiento")]
    public string TipoMovimiento { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("tipo_documento_referencia")]
    public string TipoDocumentoReferencia { get; set; } = string.Empty;

    [Column("id_documento_referencia")]
    public long IdDocumentoReferencia { get; set; }

    [Column("cantidad", TypeName = "decimal(12, 4)")]
    public decimal Cantidad { get; set; }

    [Column("stock_anterior", TypeName = "decimal(12, 4)")]
    public decimal StockAnterior { get; set; }

    [Column("stock_posterior", TypeName = "decimal(12, 4)")]
    public decimal StockPosterior { get; set; }

    [Column("costo_unitario", TypeName = "decimal(12, 4)")]
    public decimal CostoUnitario { get; set; }

    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [ForeignKey(nameof(IdUsuario))]
    public Usuario Usuario { get; set; } = null!;

    [Column("fecha_movimiento")]
    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
}