using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.Models;

namespace MaquilaBackend.Services;

public class KardexService : IKardexService
{
    private readonly MaquilaDbContext _context;

    public KardexService(MaquilaDbContext context)
    {
        _context = context;
    }

    public async Task<KardexMovimiento> RegistrarMovimientoAsync(
        int idArticulo,
        int idAlmacen,
        string tipoMovimiento,
        string tipoDocumentoReferencia,
        long idDocumentoReferencia,
        decimal cantidad,
        decimal costoUnitario,
        int idUsuario,
        bool actualizarCostoPromedio = false)
    {
        if (cantidad == 0)
            throw new InvalidOperationException("La cantidad del movimiento en Kardex no puede ser cero.");

        var articulo = await _context.Articulos.FindAsync(idArticulo)
            ?? throw new InvalidOperationException($"El artículo #{idArticulo} no existe.");

        var almacen = await _context.Almacenes.FindAsync(idAlmacen)
            ?? throw new InvalidOperationException($"El almacén #{idAlmacen} no existe.");

        // Obtener o inicializar la existencia del artículo en este almacén
        var existencia = await _context.Existencias
            .FirstOrDefaultAsync(e => e.IdArticulo == idArticulo && e.IdAlmacen == idAlmacen);

        decimal stockAnterior = existencia?.StockActual ?? 0m;
        decimal stockPosterior;

        // Determinar sentido del movimiento
        bool esEntrada = tipoMovimiento switch
        {
            "ENTRADA_COMPRA" => true,
            "ENTRADA_PRODUCIDO" => true,
            "MERMA_CORTE" => true,
            "CONSUMO_PRODUCCION" => false,
            "SALIDA_PEDIDO" => false,
            "AJUSTE_INVENTARIO" => cantidad > 0,
            _ => throw new InvalidOperationException($"Tipo de movimiento no reconocido: '{tipoMovimiento}'.")
        };

        if (tipoMovimiento == "AJUSTE_INVENTARIO")
        {
            stockPosterior = stockAnterior + cantidad;
        }
        else
        {
            decimal delta = Math.Abs(cantidad);
            stockPosterior = esEntrada ? (stockAnterior + delta) : (stockAnterior - delta);
        }

        // Validación estricta contra stocks negativos
        if (stockPosterior < 0)
        {
            throw new InvalidOperationException(
                $"Stock insuficiente en '{almacen.NombreAlmacen}' para '{articulo.NombreArticulo}'. " +
                $"Disponible: {stockAnterior}, Requerido: {Math.Abs(cantidad)}.");
        }

        // 1. Actualizar o crear registro en Existencias
        if (existencia == null)
        {
            existencia = new Existencia
            {
                IdArticulo = idArticulo,
                IdAlmacen = idAlmacen,
                StockActual = stockPosterior
            };
            _context.Existencias.Add(existencia);
        }
        else
        {
            existencia.StockActual = stockPosterior;
        }

        // 2. Recálculo de Costo Promedio Ponderado (solo en compras)
        if (actualizarCostoPromedio && esEntrada && costoUnitario > 0)
        {
            decimal stockTotalPrevio = await _context.Existencias
                .Where(e => e.IdArticulo == idArticulo)
                .SumAsync(e => e.StockActual);

            decimal costoActualTotal = stockTotalPrevio * articulo.CostoPromedio;
            decimal costoNuevaEntrada = Math.Abs(cantidad) * costoUnitario;
            decimal nuevoStockTotal = stockTotalPrevio + Math.Abs(cantidad);

            if (nuevoStockTotal > 0)
            {
                articulo.CostoPromedio = Math.Round((costoActualTotal + costoNuevaEntrada) / nuevoStockTotal, 4);
            }
        }

        // 3. Crear el asiento inmutable en Kardex
        var movimiento = new KardexMovimiento
        {
            IdArticulo = idArticulo,
            IdAlmacen = idAlmacen,
            TipoMovimiento = tipoMovimiento,
            TipoDocumentoReferencia = tipoDocumentoReferencia,
            IdDocumentoReferencia = idDocumentoReferencia,
            Cantidad = cantidad,
            StockAnterior = stockAnterior,
            StockPosterior = stockPosterior,
            CostoUnitario = costoUnitario > 0 ? costoUnitario : articulo.CostoPromedio,
            IdUsuario = idUsuario,
            FechaMovimiento = DateTime.UtcNow
        };

        _context.KardexMovimientos.Add(movimiento);
        await _context.SaveChangesAsync();

        return movimiento;
    }
}