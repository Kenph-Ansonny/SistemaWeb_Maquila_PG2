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

    //Modulo de Merma
    public DbSet<RegistroMerma> RegistroMermas => Set<RegistroMerma>();

    //Modulo de Produccion
    public DbSet<Receta> Recetas => Set<Receta>();
    public DbSet<RecetaDetalle> RecetaDetalles => Set<RecetaDetalle>();

    //Modulo Pedidos
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<PedidoDetalle> PedidoDetalles { get; set; }
    public DbSet<OrdenProduccion> OrdenesProduccion { get; set; }

    //Kardex
    public DbSet<KardexMovimiento> KardexMovimientos => Set<KardexMovimiento>();

    //Proveedores
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    //Compras
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<CompraDetalle> CompraDetalles => Set<CompraDetalle>();
    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //Modulo de seguridad
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

        // Modulo de Inventario
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

        modelBuilder.Entity<ProductoTerminadoDetalle>()
            .HasOne(p => p.Articulo)
            .WithOne(a => a.DetallePrenda)
            .HasForeignKey<ProductoTerminadoDetalle>(p => p.IdArticulo);

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

        // Modulo de Produccion - Recetas
        modelBuilder.Entity<RecetaDetalle>()
            .HasKey(rd => new { rd.IdReceta, rd.IdArticuloInsumo });

        modelBuilder.Entity<RecetaDetalle>()
            .HasOne(rd => rd.Receta)
            .WithMany(r => r.Detalles)
            .HasForeignKey(rd => rd.IdReceta)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecetaDetalle>()
            .HasOne(rd => rd.ArticuloInsumo)
            .WithMany()
            .HasForeignKey(rd => rd.IdArticuloInsumo)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecetaDetalle>()
            .HasOne(rd => rd.UnidadConsumo)
            .WithMany()
            .HasForeignKey(rd => rd.IdUnidadConsumo)
            .OnDelete(DeleteBehavior.Restrict);

        // Modulo Pedidos
        modelBuilder.Entity<Pedido>()
            .HasOne(p => p.Cliente)
            .WithMany(c => c.Pedidos)
            .HasForeignKey(p => p.IdCliente)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Pedido>()
            .HasOne(p => p.Usuario)
            .WithMany()
            .HasForeignKey(p => p.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PedidoDetalle>()
            .HasOne(pd => pd.Pedido)
            .WithMany(p => p.Detalles)
            .HasForeignKey(pd => pd.IdPedido)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PedidoDetalle>()
            .HasOne(pd => pd.ArticuloPrenda)
            .WithMany()
            .HasForeignKey(pd => pd.IdArticuloPrenda)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PedidoDetalle>()
            .HasOne(pd => pd.Receta)
            .WithMany()
            .HasForeignKey(pd => pd.IdReceta)
            .OnDelete(DeleteBehavior.Restrict);

        // Mermas
        modelBuilder.Entity<RegistroMerma>()
            .ToTable("Registro_Mermas")
            .HasKey(m => m.IdMerma);

        modelBuilder.Entity<RegistroMerma>()
            .HasOne(m => m.OrdenProduccion)
            .WithMany()
            .HasForeignKey(m => m.IdOrdenProduccion)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RegistroMerma>()
            .HasOne(m => m.ArticuloInsumo)
            .WithMany()
            .HasForeignKey(m => m.IdArticuloInsumo)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RegistroMerma>()
            .HasOne(m => m.AlmacenDestino)
            .WithMany()
            .HasForeignKey(m => m.IdAlmacenDestino)
            .OnDelete(DeleteBehavior.Restrict);

        //Kardex
        modelBuilder.Entity<KardexMovimiento>()
            .ToTable("Kardex_Movimientos")
            .HasKey(k => k.IdKardex);

        modelBuilder.Entity<KardexMovimiento>()
            .HasOne(k => k.Articulo)
            .WithMany()
            .HasForeignKey(k => k.IdArticulo)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KardexMovimiento>()
            .HasOne(k => k.Almacen)
            .WithMany()
            .HasForeignKey(k => k.IdAlmacen)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KardexMovimiento>()
            .HasOne(k => k.Usuario)
            .WithMany()
            .HasForeignKey(k => k.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        //Proveedor
        modelBuilder.Entity<Proveedor>()
            .ToTable("Proveedores")
            .HasKey(p => p.IdProveedor);
        
        //Compras
        modelBuilder.Entity<Compra>(entity =>
        {
            entity.ToTable("Compras");
            entity.HasKey(c => c.IdCompra);

            // Restricción única (id_proveedor, numero_documento)
            entity.HasIndex(c => new { c.IdProveedor, c.NumeroDocumento })
                .IsUnique()
                .HasDatabaseName("uq_compra_documento");

            entity.HasOne(c => c.Proveedor)
                .WithMany()
                .HasForeignKey(c => c.IdProveedor)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CompraDetalle>(entity =>
        {
            entity.ToTable("CompraDetalles");
            entity.HasKey(d => d.IdDetalle);

            entity.HasOne(d => d.Compra)
                .WithMany(c => c.Detalles)
                .HasForeignKey(d => d.IdCompra)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Articulo)
                .WithMany()
                .HasForeignKey(d => d.IdArticulo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.UnidadMedida)
                .WithMany()
                .HasForeignKey(d => d.IdUnidadMedida)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.AlmacenDestino)
                .WithMany()
                .HasForeignKey(d => d.IdAlmacenDestino)
                .OnDelete(DeleteBehavior.Restrict);
        });

    }
}