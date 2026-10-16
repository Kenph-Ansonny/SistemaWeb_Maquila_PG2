using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Controllers;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using Xunit;

namespace MaquilaBackend.Tests;

public class ControladoresTests
{
    private MaquilaDbContext CrearDbContextEnMemoria(string nombreBd)
    {
        var options = new DbContextOptionsBuilder<MaquilaDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBd)
            .Options;

        return new MaquilaDbContext(options);
    }

    private void SimularSesionUsuario(ControllerBase controller, int usuarioId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString())
        }, "TestAuth");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    [Fact]
    public async Task Pedidos_CambiarEstado_RegistradoACancelado_DebeActualizarYCrearBitacora()
    {
        // arrange: preparar pedido en estado registrado y sesion de usuario
        using var db = CrearDbContextEnMemoria("Test_Pedidos_Cancelar_Exito");

        var pedido = new Pedido
        {
            IdPedido = 1,
            NumeroPedido = "PED-2026-0001",
            IdCliente = 10,
            IdUsuario = 1,
            EstadoPedido = "Registrado",
            FechaEmision = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var controller = new PedidosController(db);
        SimularSesionUsuario(controller, usuarioId: 1);

        var dto = new CambiarEstadoPedidoDto("Cancelado");

        // act: cambiar el estado del pedido a cancelado
        var resultado = await controller.CambiarEstado(1, dto);

        // assert: comprobar respuesta ok 200, pedido cancelado y registro en bitacora
        var okResult = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(200, okResult.StatusCode);

        var pedidoDb = await db.Pedidos.FindAsync(1);
        Assert.NotNull(pedidoDb);
        Assert.Equal("Cancelado", pedidoDb.EstadoPedido);

        var log = await db.Bitacora.FirstOrDefaultAsync(b => b.TablaAfectada == "Pedidos" && b.IdRegistro == "1");
        Assert.NotNull(log);
        Assert.Equal("STATE_CHANGE", log.Accion);
    }

    [Fact]
    public async Task Pedidos_CambiarEstado_TransicionInvalida_DebeRetornarBadRequest()
    {
        // arrange: preparar pedido registrado e intentar pasar directo a finalizado
        using var db = CrearDbContextEnMemoria("Test_Pedidos_TransicionInvalida");

        var pedido = new Pedido
        {
            IdPedido = 2,
            NumeroPedido = "PED-2026-0002",
            IdCliente = 10,
            IdUsuario = 1,
            EstadoPedido = "Registrado",
            FechaEmision = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var controller = new PedidosController(db);
        SimularSesionUsuario(controller, usuarioId: 1);

        var dto = new CambiarEstadoPedidoDto("Finalizado");

        // act: ejecutar la transicion de estado no permitida
        var resultado = await controller.CambiarEstado(2, dto);

        // assert: comprobar que el controlador devuelve bad request 400
        var badRequest = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task Pedidos_CrearPedido_ConNumeroFormatoInvalido_DebeRetornarBadRequest()
    {
        // arrange: preparar dto con formato de pedido que contiene espacios invalidos
        using var db = CrearDbContextEnMemoria("Test_Pedidos_FormatoInvalido");

        var controller = new PedidosController(db);
        SimularSesionUsuario(controller, usuarioId: 1);

        var dto = new GuardarPedidoDto(
            "PEDIDO INVALIDO CON ESPACIOS",
            10,
            DateTime.UtcNow.AddDays(5),
            new List<GuardarPedidoDetalleDto>()
        );

        // act: llamar a la creacion del pedido
        var resultado = await controller.CrearPedido(dto);

        // assert: comprobar rechazo por formato invalido con bad request 400
        var badRequest = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task OrdenesProduccion_RegistrarAvance_SaltarEtapa_DebeRetornarBadRequest()
    {
        // arrange: preparar orden en iniciada y dto para saltar directo a en costura
        using var db = CrearDbContextEnMemoria("Test_Ordenes_SaltoEtapa");

        var pedido = new Pedido
        {
            IdPedido = 8,
            NumeroPedido = "PED-2026-0008",
            EstadoPedido = "En Produccion",
            IdCliente = 1,
            IdUsuario = 1,
            FechaEmision = DateTime.UtcNow
        };

        var detalle = new PedidoDetalle
        {
            IdDetalle = 50,
            IdPedido = 8,
            Cantidad = 100,
            PrecioUnitarioAcordado = 15.0m,
            Pedido = pedido
        };

        var orden = new OrdenProduccion
        {
            IdOrden = 100,
            IdPedidoDetalle = 50,
            PedidoDetalle = detalle,
            FechaInicio = DateTime.UtcNow,
            CantidadProgramada = 100,
            CantidadProducida = 0,
            EstadoOrden = "Iniciada"
        };

        db.Pedidos.Add(pedido);
        db.PedidoDetalles.Add(detalle);
        db.OrdenesProduccion.Add(orden);
        await db.SaveChangesAsync();

        var controller = new OrdenesProduccionController(db);
        SimularSesionUsuario(controller, usuarioId: 1);

        var dtoAvance = new RegistrarAvanceDto("En Costura", 20);

        // act: registrar avance saltandose la etapa en corte
        var resultado = await controller.RegistrarAvance(100, dtoAvance);

        // assert: comprobar que se rechaza la transicion con bad request 400
        var badRequest = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task OrdenesProduccion_CancelarOrden_OrdenIniciada_DebeCambiarACancelada()
    {
        // arrange: preparar orden vinculada en estado iniciada
        using var db = CrearDbContextEnMemoria("Test_Ordenes_Cancelar");

        var pedido = new Pedido
        {
            IdPedido = 5,
            NumeroPedido = "PED-2026-0005",
            EstadoPedido = "En Produccion",
            IdCliente = 1,
            IdUsuario = 1,
            FechaEmision = DateTime.UtcNow
        };

        var detalle = new PedidoDetalle
        {
            IdDetalle = 10,
            IdPedido = 5,
            Cantidad = 50,
            PrecioUnitarioAcordado = 20.0m,
            Pedido = pedido
        };

        var orden = new OrdenProduccion
        {
            IdOrden = 200,
            IdPedidoDetalle = 10,
            PedidoDetalle = detalle,
            FechaInicio = DateTime.UtcNow,
            CantidadProgramada = 50,
            CantidadProducida = 0,
            EstadoOrden = "Iniciada"
        };

        db.Pedidos.Add(pedido);
        db.PedidoDetalles.Add(detalle);
        db.OrdenesProduccion.Add(orden);
        await db.SaveChangesAsync();

        var controller = new OrdenesProduccionController(db);
        SimularSesionUsuario(controller, usuarioId: 1);

        // act: ejecutar la cancelacion de la orden
        var resultado = await controller.CancelarOrden(200);

        // assert: comprobar respuesta ok 200 y que la orden cambio a cancelada
        var okResult = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(200, okResult.StatusCode);

        var ordenDb = await db.OrdenesProduccion.FindAsync(200);
        Assert.NotNull(ordenDb);
        Assert.Equal("Cancelada", ordenDb.EstadoOrden);
    }
}