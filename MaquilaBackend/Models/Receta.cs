using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Recetas")]
public class Receta
{
    [Key]
    [Column("id_receta")]
    public int IdReceta { get; set; }

    [Required]
    [Column("id_articulo_prenda")]
    public int IdArticuloPrenda { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("nombre_receta")]
    public string NombreReceta { get; set; } = string.Empty;

    [Column("descripcion_receta")]
    public string? DescripcionReceta { get; set; }

    [Column("estado_receta")]
    public bool EstadoReceta { get; set; } = true;

    [ForeignKey(nameof(IdArticuloPrenda))]
    public Articulo ArticuloPrenda { get; set; } = null!;

    public ICollection<RecetaDetalle> Detalles { get; set; } = new List<RecetaDetalle>();
}