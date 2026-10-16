using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using MaquilaBackend.Services;
using MaquilaBackend.Security;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using MaquilaBackend.Helpers;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComprasController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private readonly IKardexService _kardexService;
    private const int ID_MODULO_COMPRAS = 3;

    public ComprasController(MaquilaDbContext context, IKardexService kardexService)
    {
        _context = context;
        _kardexService = kardexService;
    }

    private int ObtenerUsuarioIdSesion()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int userId))
            throw new UnauthorizedAccessException("No se pudo identificar al usuario autenticado.");
        return userId;
    }

    // GET: api/compras
    [HttpGet]
    [RequierePermiso("COMPRAS", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<CompraListDto>>> GetCompras(
        [FromQuery] string? estado = null,
        [FromQuery] int? idProveedor = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? busqueda = null)
    {
        var query = _context.Compras
            .Include(c => c.Proveedor)
            .Include(c => c.Usuario)
            .Include(c => c.Detalles)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(c => c.EstadoCompra == estado.Trim());

        if (idProveedor.HasValue && idProveedor.Value > 0)
            query = query.Where(c => c.IdProveedor == idProveedor.Value);

        if (fechaDesde.HasValue)
            query = query.Where(c => c.FechaCompra >= fechaDesde.Value.Date);

        if (fechaHasta.HasValue)
            query = query.Where(c => c.FechaCompra <= fechaHasta.Value.Date.AddDays(1).AddTicks(-1));

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(c =>
                c.NumeroDocumento.ToLower().Contains(term) ||
                c.Proveedor.NombreProveedor.ToLower().Contains(term));
        }

        var lista = await query
            .OrderByDescending(c => c.FechaCompra)
            .Select(c => new CompraListDto(
                c.IdCompra,
                c.NumeroDocumento,
                c.IdProveedor,
                c.Proveedor.NombreProveedor,
                c.IdUsuario,
                c.Usuario.NombreUsuario,
                c.FechaCompra,
                c.EstadoCompra,
                c.CostoTotal,
                c.Detalles.Count,
                c.Detalles.Sum(d => d.Cantidad)
            ))
            .ToListAsync();

        return Ok(lista);
    }

    // GET: api/compras/{id}
    [HttpGet("{id}")]
    [RequierePermiso("COMPRAS", AccionPermiso.Consultar)]
    public async Task<ActionResult<CompraCompletaDto>> GetCompraPorId(int id)
    {
        var compra = await _context.Compras
            .Include(c => c.Proveedor)
            .Include(c => c.Usuario)
            .Include(c => c.Detalles)
                .ThenInclude(d => d.Articulo)
            .Include(c => c.Detalles)
                .ThenInclude(d => d.UnidadMedida)
            .Include(c => c.Detalles)
                .ThenInclude(d => d.AlmacenDestino)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCompra == id);

        if (compra == null)
            return NotFound(new { message = "La compra solicitada no existe." });

        var detallesDto = compra.Detalles.Select(d => new CompraDetalleDto(
            d.IdDetalle,
            d.IdArticulo,
            d.Articulo.CodigoArticulo,
            d.Articulo.NombreArticulo,
            d.Articulo.TipoArticulo,
            d.IdUnidadMedida,
            d.UnidadMedida.CodigoMedida,
            d.Cantidad,
            d.CostoUnitario,
            d.Subtotal,
            d.IdAlmacenDestino,
            d.AlmacenDestino.NombreAlmacen
        )).ToList();

        var resultado = new CompraCompletaDto(
            compra.IdCompra,
            compra.NumeroDocumento,
            compra.IdProveedor,
            compra.Proveedor.NombreProveedor,
            compra.Proveedor.Telefono,
            compra.Proveedor.Direccion,
            compra.IdUsuario,
            compra.Usuario.NombreUsuario,
            compra.FechaCompra,
            compra.EstadoCompra,
            compra.CostoTotal,
            detallesDto
        );

        return Ok(resultado);
    }

    // POST: api/compras
    [HttpPost]
    [RequierePermiso("COMPRAS", AccionPermiso.Insertar)]
    public async Task<IActionResult> RegistrarCompra([FromBody] GuardarCompraDto dto)
    {
        var docLimpio = dto.NumeroDocumento?.Trim().ToUpper() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(docLimpio) || docLimpio.Length < 2 || docLimpio.Length > 50)
            return BadRequest(new { message = "El número de documento / factura debe tener entre 2 y 50 caracteres." });

        var proveedor = await _context.Proveedores.FindAsync(dto.IdProveedor);
        if (proveedor == null || !proveedor.EstadoProveedor)
            return BadRequest(new { message = "El proveedor seleccionado no existe o está inactivo." });

        // Validación uq_compra_documento
        bool existeDoc = await _context.Compras.AnyAsync(c => 
            c.IdProveedor == dto.IdProveedor && c.NumeroDocumento == docLimpio);
        if (existeDoc)
            return BadRequest(new { message = $"El documento '{docLimpio}' ya fue registrado previamente para este proveedor." });

        if (dto.Detalles == null || dto.Detalles.Count == 0)
            return BadRequest(new { message = "Debes ingresar al menos un artículo o material en la compra." });

        // Validaciones por línea
        foreach (var (linea, idx) in dto.Detalles.Select((l, i) => (l, i + 1)))
        {
            if (linea.Cantidad <= 0)
                return BadRequest(new { message = $"Línea #{idx}: La cantidad ingresada debe ser mayor a cero." });

            if (linea.CostoUnitario < 0)
                return BadRequest(new { message = $"Línea #{idx}: El costo unitario no puede ser negativo." });

            var articulo = await _context.Articulos.FindAsync(linea.IdArticulo);
            if (articulo == null || !articulo.EstadoArticulo)
                return BadRequest(new { message = $"Línea #{idx}: El artículo seleccionado no existe o está inactivo." });

            var unidad = await _context.UnidadesMedida.FindAsync(linea.IdUnidadMedida);
            if (unidad == null)
                return BadRequest(new { message = $"Línea #{idx}: La unidad de medida seleccionada no existe." });

            var almacen = await _context.Almacenes.FindAsync(linea.IdAlmacenDestino);
            if (almacen == null || !almacen.EstadoAlmacen)
                return BadRequest(new { message = $"Línea #{idx}: La bodega de destino no existe o está inactiva." });
        }

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevaCompra = new Compra
            {
                NumeroDocumento = docLimpio,
                IdProveedor = dto.IdProveedor,
                IdUsuario = idUsuario,
                FechaCompra = dto.FechaCompra == default ? DateTime.UtcNow : dto.FechaCompra,
                EstadoCompra = "Pendiente"
            };

            decimal totalCalculado = 0;

            foreach (var item in dto.Detalles)
            {
                decimal subtotal = Math.Round(item.Cantidad * item.CostoUnitario, 2);
                totalCalculado += subtotal;

                nuevaCompra.Detalles.Add(new CompraDetalle
                {
                    IdArticulo = item.IdArticulo,
                    IdUnidadMedida = item.IdUnidadMedida,
                    Cantidad = Math.Round(item.Cantidad, 2),
                    CostoUnitario = Math.Round(item.CostoUnitario, 4),
                    Subtotal = subtotal,
                    IdAlmacenDestino = item.IdAlmacenDestino
                });
            }

            nuevaCompra.CostoTotal = totalCalculado;

            _context.Compras.Add(nuevaCompra);
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = ID_MODULO_COMPRAS,
                Accion = "INSERT",
                TablaAfectada = "Compras",
                IdRegistro = nuevaCompra.IdCompra.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Compra registrada en estado 'Pendiente'.", idCompra = nuevaCompra.IdCompra });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al registrar la compra: " + ex.Message });
        }
    }

    // POST: api/compras/{id}/recibir (DISPARADOR KARDEX Y EXISTENCIAS)
    [HttpPost("{id}/recibir")]
    [RequierePermiso("COMPRAS", AccionPermiso.Modificar)]
    public async Task<IActionResult> RecibirCompra(int id)
    {
        var compra = await _context.Compras
            .Include(c => c.Detalles)
                .ThenInclude(d => d.Articulo)
            .FirstOrDefaultAsync(c => c.IdCompra == id);

        if (compra == null)
            return NotFound(new { message = "Compra no encontrada." });

        if (compra.EstadoCompra == "Recibido")
            return BadRequest(new { message = "Esta compra ya fue recibida e ingresada al inventario anteriormente." });

        if (compra.EstadoCompra == "Anulado")
            return BadRequest(new { message = "No se puede recibir una compra que se encuentra anulada." });

        if (compra.Detalles.Count == 0)
            return BadRequest(new { message = "La compra no cuenta con detalles para dar ingreso a bodega." });

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // INTEGRACIÓN KARDEX: Asienta cada línea, actualiza existencias y recalcula CPP
            foreach (var det in compra.Detalles)
            {
                await _kardexService.RegistrarMovimientoAsync(
                    idArticulo: det.IdArticulo,
                    idAlmacen: det.IdAlmacenDestino,
                    tipoMovimiento: "ENTRADA_COMPRA",
                    tipoDocumentoReferencia: "COMPRAS",
                    idDocumentoReferencia: compra.IdCompra,
                    cantidad: det.Cantidad,
                    costoUnitario: det.CostoUnitario,
                    idUsuario: idUsuario,
                    actualizarCostoPromedio: true // Recálculo formal de Costo Promedio Ponderado
                );
            }

            var estadoAnterior = compra.EstadoCompra;
            compra.EstadoCompra = "Recibido";

            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = ID_MODULO_COMPRAS,
                Accion = "RECEPCION_COMPRA",
                TablaAfectada = "Compras",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(new { EstadoCompra = estadoAnterior }),
                ValoresNuevos = JsonSerializer.Serialize(new { EstadoCompra = "Recibido" }),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Mercadería recibida exitosamente. Inventario y Kardex actualizados." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al recibir la compra: " + ex.Message });
        }
    }

    // PATCH: api/compras/{id}/anular
    [HttpPatch("{id}/anular")]
    [RequierePermiso("COMPRAS", AccionPermiso.Eliminar)]
    public async Task<IActionResult> AnularCompra(int id)
    {
        var compra = await _context.Compras
            .Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.IdCompra == id);

        if (compra == null)
            return NotFound(new { message = "Compra no encontrada." });

        if (compra.EstadoCompra == "Anulado")
            return BadRequest(new { message = "La compra ya está anulada." });

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Si estaba recibida, valida existencias y descarga del Kardex
            if (compra.EstadoCompra == "Recibido")
            {
                foreach (var det in compra.Detalles)
                {
                    var existencia = await _context.Existencias
                        .FirstOrDefaultAsync(e => e.IdArticulo == det.IdArticulo && e.IdAlmacen == det.IdAlmacenDestino);

                    if (existencia == null || existencia.StockActual < det.Cantidad)
                    {
                        return BadRequest(new
                        {
                            message = $"No se puede anular la compra porque parte del material ya fue consumido en producción o retirado de bodega."
                        });
                    }

                    await _kardexService.RegistrarMovimientoAsync(
                        idArticulo: det.IdArticulo,
                        idAlmacen: det.IdAlmacenDestino,
                        tipoMovimiento: "AJUSTE_INVENTARIO",
                        tipoDocumentoReferencia: "ANULACION_COMPRA",
                        idDocumentoReferencia: compra.IdCompra,
                        cantidad: -det.Cantidad, // Salida por anulación
                        costoUnitario: det.CostoUnitario,
                        idUsuario: idUsuario
                    );
                }
            }

            var estadoAnterior = compra.EstadoCompra;
            compra.EstadoCompra = "Anulado";

            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = ID_MODULO_COMPRAS,
                Accion = "ANULAR",
                TablaAfectada = "Compras",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(new { EstadoCompra = estadoAnterior }),
                ValoresNuevos = JsonSerializer.Serialize(new { EstadoCompra = "Anulado" }),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Compra anulada correctamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al anular la compra: " + ex.Message });
        }
    }
}