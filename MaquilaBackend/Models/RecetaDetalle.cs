using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Receta_Detalles")]
public class RecetaDetalle
{
    [Column("id_receta")]
    public int IdReceta { get; set; }

    [Column("id_articulo_insumo")]
    public int IdArticuloInsumo { get; set; }

    [Column("id_unidad_consumo")]
    public int IdUnidadConsumo { get; set; }

    [Column("cantidad_neta")]
    public decimal CantidadNeta { get; set; }

    [Column("porcentaje_merma")]
    public decimal PorcentajeMerma { get; set; } = 0.00m;

    [Column("cantidad_bruta")]
    public decimal CantidadBruta { get; set; }

    [ForeignKey(nameof(IdReceta))]
    public Receta Receta { get; set; } = null!;

    [ForeignKey(nameof(IdArticuloInsumo))]
    public Articulo ArticuloInsumo { get; set; } = null!;

    [ForeignKey(nameof(IdUnidadConsumo))]
    public UnidadMedida UnidadConsumo { get; set; } = null!;
}