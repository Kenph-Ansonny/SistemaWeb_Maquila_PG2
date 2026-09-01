using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Permisos_Rol")]
public class PermisoRol
{
    [Column("id_rol")]
    public int IdRol { get; set; }

    [ForeignKey(nameof(IdRol))]
    public Rol Rol { get; set; } = null!;

    [Column("id_modulo")]
    public int IdModulo { get; set; }

    [ForeignKey(nameof(IdModulo))]
    public Modulo Modulo { get; set; } = null!;

    [Column("puede_consultar")]
    public bool PuedeConsultar { get; set; }

    [Column("puede_insertar")]
    public bool PuedeInsertar { get; set; }

    [Column("puede_modificar")]
    public bool PuedeModificar { get; set; }

    [Column("puede_eliminar")]
    public bool PuedeEliminar { get; set; }
}