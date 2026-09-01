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
    public DbSet<Bitacora> Bitacora => Set<Bitacora>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Configuración de la tabla pivote Usuario_Roles
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

        // 2. Configuración de la tabla pivote Permisos_Rol (Llave y Relaciones Foráneas)
        modelBuilder.Entity<PermisoRol>()
            .HasKey(pr => new { pr.IdRol, pr.IdModulo });

        modelBuilder.Entity<PermisoRol>()
            .HasOne(pr => pr.Rol)
            .WithMany(r => r.PermisosRol)
            .HasForeignKey(pr => pr.IdRol);

        modelBuilder.Entity<PermisoRol>()
            .HasOne(pr => pr.Modulo)
            .WithMany()
            .HasForeignKey(pr => pr.IdModulo);
    }
}