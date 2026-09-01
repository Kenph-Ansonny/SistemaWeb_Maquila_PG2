using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Roles")]
public class Rol
{
    [Key]
    [Column("id_rol")]
    public int IdRol { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("nombre_rol")]
    public string NombreRol { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("descripcion")]
    public string? Descripcion { get; set; }

    [Column("estado_rol")]
    public bool EstadoRol { get; set; } = true;

    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
    public ICollection<PermisoRol> PermisosRol { get; set; } = new List<PermisoRol>();
}