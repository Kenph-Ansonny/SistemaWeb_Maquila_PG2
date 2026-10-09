using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Clientes")]
public class Cliente
{
    [Key]
    [Column("id_cliente")]
    public int IdCliente { get; set; }

    [Column("nombre_cliente")]
    public string NombreCliente { get; set; } = string.Empty;

    [Column("telefono_cliente")]
    public string? TelefonoCliente { get; set; }

    [Column("direccion_cliente")]
    public string? DireccionCliente { get; set; }

    [Column("estado_cliente")]
    public bool EstadoCliente { get; set; } = true;

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}