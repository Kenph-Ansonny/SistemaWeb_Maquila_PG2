using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Usuario_Roles")]
public class UsuarioRol
{
    [Column("id_usuario")]
    public int IdUsuario { get; set; }
    public Usuario Usuario { get; set; } = null!;

    [Column("id_rol")]
    public int IdRol { get; set; }
    public Rol Rol { get; set; } = null!;
}