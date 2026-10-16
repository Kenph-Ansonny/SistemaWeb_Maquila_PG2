using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using MaquilaBackend.Security; // <-- Importante para RequierePermiso

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public DashboardController(MaquilaDbContext context)
    {
        _context = context;
    }

    [HttpGet("resumen")]
    [RequierePermiso("DASHBOARD", AccionPermiso.Consultar)] //Restringe por módulo y acción
    public async Task<ActionResult<DashboardResumenDto>> GetResumen([FromQuery] string periodo = "mes")
    {
        var periodoNormalizado = periodo.ToLower().Trim();
        var ahora = DateTime.UtcNow;

        DateTime fechaInicioFiltro = periodoNormalizado switch
        {
            "semana" => ahora.AddDays(-56), // Últimas 8 semanas
            "anio" => ahora.AddYears(-5),   // Últimos 5 años
            _ => ahora.AddMonths(-12)       // Últimos 12 meses por defecto
        };

        // 1. Consulta de Pedidos e Ingresos (excluyendo Cancelados)
        var pedidosRaw = await _context.Pedidos
            .Where(p => p.EstadoPedido != "Cancelado" && p.FechaEmision >= fechaInicioFiltro)
            .Select(p => new
            {
                p.IdPedido,
                p.FechaEmision,
                TotalMonto = p.Detalles.Sum(d => d.Cantidad * d.PrecioUnitarioAcordado),
                TotalPrendas = p.Detalles.Sum(d => d.Cantidad)
            })
            .ToListAsync();

        // 2. Agrupación temporal según el filtro
        List<IngresoPeriodoPuntoDto> puntosGrafico = new();
        var culturaEs = new CultureInfo("es-GT");

        if (periodoNormalizado == "semana")
        {
            puntosGrafico = pedidosRaw
                .GroupBy(p => ISOWeek.GetWeekOfYear(p.FechaEmision))
                .OrderBy(g => g.Key)
                .Select(g => new IngresoPeriodoPuntoDto(
                    Etiqueta: $"Semana {g.Key}",
                    Ingresos: Math.Round(g.Sum(x => x.TotalMonto), 2),
                    CantidadPedidos: g.Count(),
                    TotalPrendas: g.Sum(x => x.TotalPrendas)
                )).ToList();
        }
        else if (periodoNormalizado == "anio")
        {
            puntosGrafico = pedidosRaw
                .GroupBy(p => p.FechaEmision.Year)
                .OrderBy(g => g.Key)
                .Select(g => new IngresoPeriodoPuntoDto(
                    Etiqueta: g.Key.ToString(),
                    Ingresos: Math.Round(g.Sum(x => x.TotalMonto), 2),
                    CantidadPedidos: g.Count(),
                    TotalPrendas: g.Sum(x => x.TotalPrendas)
                )).ToList();
        }
        else
        {
            // Agrupación mensual
            puntosGrafico = pedidosRaw
                .GroupBy(p => new { p.FechaEmision.Year, p.FechaEmision.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new IngresoPeriodoPuntoDto(
                    Etiqueta: $"{culturaEs.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month).ToUpper()} {g.Key.Year}",
                    Ingresos: Math.Round(g.Sum(x => x.TotalMonto), 2),
                    CantidadPedidos: g.Count(),
                    TotalPrendas: g.Sum(x => x.TotalPrendas)
                )).ToList();
        }

        // Cuellos de Botella y Embudo de Producción de Órdenes no canceladas
        var ordenesProduccion = await _context.OrdenesProduccion
            .Where(o => o.EstadoOrden != "Cancelada" && o.EstadoOrden != "Cancelado")
            .Select(o => new { o.IdOrden, o.EstadoOrden, o.CantidadProgramada })
            .ToListAsync();

        var etapasTaller = new[] { "Iniciada", "En Corte", "En Costura", "Terminada" };
        int totalOrdenesActivas = ordenesProduccion.Count;

        var embudo = etapasTaller.Select(etapa =>
        {
            var ordenesEnEtapa = ordenesProduccion.Where(o => o.EstadoOrden == etapa).ToList();
            var totalOrdenes = ordenesEnEtapa.Count;
            var prendas = ordenesEnEtapa.Sum(o => o.CantidadProgramada);
            var pct = totalOrdenesActivas > 0 
                ? Math.Round(((decimal)totalOrdenes / totalOrdenesActivas) * 100, 1) 
                : 0m;

            return new EmbudoEtapaDto(
                Etapa: etapa,
                TotalOrdenes: totalOrdenes,
                TotalPrendas: prendas,
                PorcentajeDelTotal: pct
            );
        }).ToList();

        // KPIs de resumen 
        var kpis = new DashboardKpisDto(
            TotalIngresosPeriodo: Math.Round(pedidosRaw.Sum(p => p.TotalMonto), 2),
            TotalPedidosPeriodo: pedidosRaw.Count,
            TotalPrendasProgramadas: pedidosRaw.Sum(p => p.TotalPrendas),
            OrdenesEnTaller: ordenesProduccion.Count(o => o.EstadoOrden == "En Corte" || o.EstadoOrden == "En Costura")
        );

        return Ok(new DashboardResumenDto(kpis, puntosGrafico, embudo));
    }
}