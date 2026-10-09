using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Pedido_Detalles")]
public class PedidoDetalle
{
    [Key]
    [Column("id_detalle")]
    public long IdDetalle { get; set; }

    [Column("id_pedido")]
    public int IdPedido { get; set; }

    [ForeignKey(nameof(IdPedido))]
    public Pedido Pedido { get; set; } = null!;

    [Column("id_articulo_prenda")]
    public int IdArticuloPrenda { get; set; }

    [ForeignKey(nameof(IdArticuloPrenda))]
    public Articulo ArticuloPrenda { get; set; } = null!;

    [Column("id_receta")]
    public int IdReceta { get; set; }

    [ForeignKey(nameof(IdReceta))]
    public Receta Receta { get; set; } = null!;

    [Column("cantidad")]
    public int Cantidad { get; set; }

    [Column("precio_unitario_acordado", TypeName = "decimal(12, 2)")]
    public decimal PrecioUnitarioAcordado { get; set; }

    public ICollection<OrdenProduccion> OrdenesProduccion { get; set; } = new List<OrdenProduccion>();
}