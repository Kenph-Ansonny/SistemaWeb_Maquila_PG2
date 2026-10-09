using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Pedidos")]
public class Pedido
{
    [Key]
    [Column("id_pedido")]
    public int IdPedido { get; set; }

    [Required]
    [MaxLength(30)]
    [Column("numero_pedido")]
    public string NumeroPedido { get; set; } = string.Empty;

    [Column("id_cliente")]
    public int IdCliente { get; set; }

    [ForeignKey(nameof(IdCliente))]
    public Cliente Cliente { get; set; } = null!;

    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [ForeignKey(nameof(IdUsuario))]
    public Usuario Usuario { get; set; } = null!;

    [Column("fecha_emision")]
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;

    [Column("fecha_estimada_entrega")]
    public DateTime? FechaEstimadaEntrega { get; set; }

    // Valores: Registrado, En Produccion, Finalizado, Entregado, Cancelado
    [Required]
    [MaxLength(30)]
    [Column("estado_pedido")]
    public string EstadoPedido { get; set; } = "Registrado";

    public ICollection<PedidoDetalle> Detalles { get; set; } = new List<PedidoDetalle>();
}