using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Compras")]
public class Compra
{
    [Key]
    [Column("id_compra")]
    public int IdCompra { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("numero_documento")]
    public string NumeroDocumento { get; set; } = string.Empty;

    [Column("id_proveedor")]
    public int IdProveedor { get; set; }

    [ForeignKey(nameof(IdProveedor))]
    public Proveedor Proveedor { get; set; } = null!;

    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [ForeignKey(nameof(IdUsuario))]
    public Usuario Usuario { get; set; } = null!;

    [Column("fecha_compra")]
    public DateTime FechaCompra { get; set; } = DateTime.UtcNow;

    // Valores: 'Pendiente', 'Recibido', 'Anulado'
    [Required]
    [MaxLength(20)]
    [Column("estado_compra")]
    public string EstadoCompra { get; set; } = "Pendiente";

    [Column("costo_total", TypeName = "decimal(14, 2)")]
    public decimal CostoTotal { get; set; } = 0.00m;

    public ICollection<CompraDetalle> Detalles { get; set; } = new List<CompraDetalle>();
}