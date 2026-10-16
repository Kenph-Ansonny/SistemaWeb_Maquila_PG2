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

    // Agrupa varias órdenes (de distintas prendas del mismo pedido) creadas juntas
    // en una misma sesión de trabajo, p. ej. cortar 2 pantalones + 4 camisas a la vez.
    // Se guarda el IdOrden de la primera orden del grupo como identificador del lote.
    // Null = orden individual, sin lote. Requiere la columna nueva (ver DDL enviado).
    [Column("id_lote")]
    public int? IdLote { get; set; }

    [Column("fecha_inicio")]
    public DateTime FechaInicio { get; set; }

    [Column("fecha_fin")]
    public DateTime? FechaFin { get; set; }

    [Column("cantidad_programada")]
    public int CantidadProgramada { get; set; }

    [Column("cantidad_producida")]
    public int CantidadProducida { get; set; } = 0;

    [Column("estado_orden")]
    public string EstadoOrden { get; set; } = "Iniciada";
}