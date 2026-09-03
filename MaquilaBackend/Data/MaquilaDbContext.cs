using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Models;

namespace MaquilaBackend.Data;

public class MaquilaDbContext : DbContext
{
    public MaquilaDbContext(DbContextOptions<MaquilaDbContext> options) : base(options) { }

    //Modulo de seguridad
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<PermisoRol> PermisosRol => Set<PermisoRol>();
    public DbSet<Bitacora> Bitacora => Set<Bitacora>();

    //Modulo de Inventario
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<UnidadConversion> UnidadConversiones => Set<UnidadConversion>();
    public DbSet<Almacen> Almacenes => Set<Almacen>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Existencia> Existencias => Set<Existencia>();
    public DbSet<ProductoTerminadoDetalle> ProductoTerminadoDetalles => Set<ProductoTerminadoDetalle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        //Modulo de seguridad
        // 1 Configuración de la tabla pivote Usuario_Roles
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

        // 2 Configuración de la tabla pivote Permisos_Rol PK y FK
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

        // Modulo deInventario
        modelBuilder.Entity<Existencia>()
            .HasKey(e => new { e.IdArticulo, e.IdAlmacen });

        modelBuilder.Entity<Existencia>()
            .HasOne(e => e.Articulo)
            .WithMany(a => a.Existencias)
            .HasForeignKey(e => e.IdArticulo);

        modelBuilder.Entity<Existencia>()
            .HasOne(e => e.Almacen)
            .WithMany(a => a.Existencias)
            .HasForeignKey(e => e.IdAlmacen);

        // Relación 1 a 1 Articulo 
        modelBuilder.Entity<ProductoTerminadoDetalle>()
            .HasOne(p => p.Articulo)
            .WithOne(a => a.DetallePrenda)
            .HasForeignKey<ProductoTerminadoDetalle>(p => p.IdArticulo);

        // Unidad onversion
        modelBuilder.Entity<UnidadConversion>()
            .HasKey(uc => new { uc.IdUnidadOrigen, uc.IdUnidadDestino });

        modelBuilder.Entity<UnidadConversion>()
            .HasOne(uc => uc.UnidadOrigen)
            .WithMany()
            .HasForeignKey(uc => uc.IdUnidadOrigen)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UnidadConversion>()
            .HasOne(uc => uc.UnidadDestino)
            .WithMany()
            .HasForeignKey(uc => uc.IdUnidadDestino)
            .OnDelete(DeleteBehavior.Restrict);
            }
}