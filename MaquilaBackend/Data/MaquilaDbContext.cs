using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Models;

namespace MaquilaBackend.Data;

public class MaquilaDbContext : DbContext
{
    public MaquilaDbContext(DbContextOptions<MaquilaDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<PermisoRol> PermisosRol => Set<PermisoRol>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Llave compuesta para Usuario_Roles
        modelBuilder.Entity<UsuarioRol>()
            .HasKey(ur => new { ur.IdUsuario, ur.IdRol });

        modelBuilder.Entity<UsuarioRol>()
            .HasOne(ur => ur.Usuario)
            .WithMany(u => u.UsuarioRoles)
            .HasForeignKey(ur => ur.IdUsuario);

        modelBuilder.Entity<UsuarioRol>()
            .HasOne(ur => ur.Rol)
            .WithMany(r => r.UsuarioRoles)
            .HasForeignKey(ur => ur.IdRol);

        // Llave compuesta para Permisos_Rol
        modelBuilder.Entity<PermisoRol>()
            .HasKey(pr => new { pr.IdRol, pr.IdModulo });
    }
}