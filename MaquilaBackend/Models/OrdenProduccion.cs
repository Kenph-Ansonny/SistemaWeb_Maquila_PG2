using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Ordenes_Produccion")]
public class OrdenProduccion
{
    [Key]
    [Column("id_orden")]
    public int IdOrden { get; set; }

    [Column("id_pedido_detalle")]
    public long IdPedidoDetalle { get; set; }

    [ForeignKey(nameof(IdPedidoDetalle))]
    public PedidoDetalle PedidoDetalle { get; set; } = null!;

    [Column("fecha_inicio")]
    public DateTime FechaInicio { get; set; }

    [Column("fecha_fin")]
    public DateTime? FechaFin { get; set; }

    [Column("cantidad_programada")]
    public int CantidadProgramada { get; set; }

    [Column("cantidad_producida")]
    public int CantidadProducida { get; set; } = 0;

    // Valores permitidos (ENUM en BD): Iniciada, En Corte, En Costura, Terminada
    [Column("estado_orden")]
    public string EstadoOrden { get; set; } = "Iniciada";
}