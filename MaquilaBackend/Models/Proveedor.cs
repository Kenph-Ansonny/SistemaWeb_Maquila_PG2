using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Proveedores")]
public class Proveedor
{
    [Key]
    [Column("id_proveedor")]
    public int IdProveedor { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("nombre_proveedor")]
    public string NombreProveedor { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("contacto_principal")]
    public string? ContactoPrincipal { get; set; }

    [MaxLength(30)]
    [Column("telefono")]
    public string? Telefono { get; set; }

    [MaxLength(200)]
    [Column("direccion")]
    public string? Direccion { get; set; }

    [Column("estado_proveedor")]
    public bool EstadoProveedor { get; set; } = true;
}