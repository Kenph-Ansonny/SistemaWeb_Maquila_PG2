using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Almacenes")]
public class Almacen
{
    [Key]
    [Column("id_almacen")]
    public int IdAlmacen { get; set; }

    [Required]
    [MaxLength(80)]
    [Column("nombre_almacen")]
    public string NombreAlmacen { get; set; } = string.Empty;

    [Column("estado_almacen")]
    public bool EstadoAlmacen { get; set; } = true;

    public ICollection<Existencia> Existencias { get; set; } = new List<Existencia>();
}