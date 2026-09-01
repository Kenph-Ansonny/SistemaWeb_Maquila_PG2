using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Modulos")]
public class Modulo
{
    [Key]
    [Column("id_modulo")]
    public int IdModulo { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("codigo_modulo")]
    public string CodigoModulo { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("nombre_modulo")]
    public string NombreModulo { get; set; } = string.Empty;

    [Column("estado_modulo")]
    public bool EstadoModulo { get; set; } = true;
}