using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Unidad_Conversiones")]
public class UnidadConversion
{
    [Column("id_unidad_origen")]
    public int IdUnidadOrigen { get; set; }

    [Column("id_unidad_destino")]
    public int IdUnidadDestino { get; set; }

    [Column("factor_conversion")]
    public decimal FactorConversion { get; set; }

    [ForeignKey(nameof(IdUnidadOrigen))]
    public UnidadMedida UnidadOrigen { get; set; } = null!;

    [ForeignKey(nameof(IdUnidadDestino))]
    public UnidadMedida UnidadDestino { get; set; } = null!;
}