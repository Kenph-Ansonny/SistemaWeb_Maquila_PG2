using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Bitacora")]
public class Bitacora
{
    [Key]
    [Column("id_bitacora")]
    public long IdBitacora { get; set; }

    [Required]
    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Required]
    [Column("id_modulo")]
    public int IdModulo { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("accion")]
    public string Accion { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("tabla_afectada")]
    public string TablaAfectada { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("id_registro")]
    public string IdRegistro { get; set; } = string.Empty;

    [Column("valores_anteriores")]
    public string? ValoresAnteriores { get; set; }

    [Column("valores_nuevos")]
    public string? ValoresNuevos { get; set; }

    [MaxLength(45)]
    [Column("direccion_ip")]
    public string? DireccionIp { get; set; }

    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Propiedades de navegación
    [ForeignKey(nameof(IdUsuario))]
    public Usuario Usuario { get; set; } = null!;

    [ForeignKey(nameof(IdModulo))]
    public Modulo Modulo { get; set; } = null!;
}