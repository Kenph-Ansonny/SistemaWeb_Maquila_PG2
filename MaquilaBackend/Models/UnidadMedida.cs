using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Unidades_Medida")]
public class UnidadMedida
{
    [Key]
    [Column("id_unidad_medida")]
    public int IdUnidadMedida { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("codigo_medida")]
    public string CodigoMedida { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("nombre_medida")]
    public string NombreMedida { get; set; } = string.Empty;

    [Required]
    [Column("tipo_medida")]
    public string TipoMedida { get; set; } = string.Empty;

    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();
}