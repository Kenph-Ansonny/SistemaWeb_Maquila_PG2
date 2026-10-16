using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaquilaBackend.Models;

[Table("Password_Reset_Tokens")]
public class PasswordResetToken
{
    [Key]
    [Column("id_token")]
    public int IdToken { get; set; }

    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [ForeignKey(nameof(IdUsuario))]
    public Usuario Usuario { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    [Column("token")]
    public string Token { get; set; } = string.Empty;

    [Column("fecha_expiracion")]
    public DateTime FechaExpiracion { get; set; }

    [Column("usado")]
    public bool Usado { get; set; } = false;
}