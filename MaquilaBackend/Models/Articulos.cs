using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Articulos")]
public class Articulo
{
    [Key]
    [Column("id_articulo")]
    public int IdArticulo { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("codigo_articulo")]
    public string CodigoArticulo { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("nombre_articulo")]
    public string NombreArticulo { get; set; } = string.Empty;

    [Required]
    [Column("tipo_articulo")]
    public string TipoArticulo { get; set; } = string.Empty;

    [Required]
    [Column("id_unidad_base_medida")]
    public int IdUnidadBaseMedida { get; set; }

    [Column("stock_minimo")]
    public decimal StockMinimo { get; set; } = 0.00m;

    [Column("costo_promedio")]
    public decimal CostoPromedio { get; set; } = 0.0000m;

    [Column("estado_articulo")]
    public bool EstadoArticulo { get; set; } = true;

    [ForeignKey(nameof(IdUnidadBaseMedida))]
    public UnidadMedida UnidadMedida { get; set; } = null!;

    public ProductoTerminadoDetalle? DetallePrenda { get; set; }
    public ICollection<Existencia> Existencias { get; set; } = new List<Existencia>();
}