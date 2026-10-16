using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Registro_Mermas")]
public class RegistroMerma
{
    [Key]
    [Column("id_merma")]
    public long IdMerma { get; set; }

    [Column("id_orden_produccion")]
    public int IdOrdenProduccion { get; set; }

    [ForeignKey(nameof(IdOrdenProduccion))]
    public OrdenProduccion OrdenProduccion { get; set; } = null!;

    [Column("id_articulo_insumo")]
    public int IdArticuloInsumo { get; set; }

    [ForeignKey(nameof(IdArticuloInsumo))]
    public Articulo ArticuloInsumo { get; set; } = null!;

    [Column("cantidad_merma_real", TypeName = "decimal(12, 4)")]
    public decimal CantidadMermaReal { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("tipo_merma")]
    public string TipoMerma { get; set; } = string.Empty;

    [Column("id_almacen_destino")]
    public int? IdAlmacenDestino { get; set; }

    [ForeignKey(nameof(IdAlmacenDestino))]
    public Almacen? AlmacenDestino { get; set; }

    [Column("observaciones")]
    public string? Observaciones { get; set; }

    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}