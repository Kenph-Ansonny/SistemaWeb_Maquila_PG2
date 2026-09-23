using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public class BitacoraController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public BitacoraController(MaquilaDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BitacoraItemDto>>> GetBitacora([FromQuery] BitacoraFiltroDto filtro)
    {
        var query = _context.Bitacora
            .Include(b => b.Usuario)
            .Include(b => b.Modulo)
            .AsNoTracking()
            .AsQueryable();

        // 1. Filtro por término general (Usuario, Tabla, Registro)
        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var term = filtro.Busqueda.Trim().ToLower();
            query = query.Where(b => 
                b.Usuario.NombreUsuario.ToLower().Contains(term) ||
                b.TablaAfectada.ToLower().Contains(term) ||
                b.IdRegistro.ToLower().Contains(term));
        }

        // 2. Filtro por Módulo
        if (!string.IsNullOrWhiteSpace(filtro.Modulo))
        {
            query = query.Where(b => b.Modulo.CodigoModulo == filtro.Modulo);
        }

        // 3. Filtro por Tipo de Acción (INSERT, UPDATE, DELETE, etc.)
        if (!string.IsNullOrWhiteSpace(filtro.Accion))
        {
            query = query.Where(b => b.Accion == filtro.Accion);
        }

        // 4. Filtro por Rango de Fechas
        if (filtro.FechaInicio.HasValue)
        {
            query = query.Where(b => b.FechaRegistro >= filtro.FechaInicio.Value.Date);
        }

        if (filtro.FechaFin.HasValue)
        {
            var fechaFinAjustada = filtro.FechaFin.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(b => b.FechaRegistro <= fechaFinAjustada);
        }

        var resultados = await query
            .OrderByDescending(b => b.FechaRegistro)
            .Take(150)
            .Select(b => new BitacoraItemDto(
                b.IdBitacora,
                b.IdUsuario,
                b.Usuario.NombreUsuario,
                b.Modulo.NombreModulo,
                b.Accion,
                b.TablaAfectada,
                b.IdRegistro,
                b.ValoresAnteriores,
                b.ValoresNuevos,
                b.DireccionIp,
                b.FechaRegistro
            ))
            .ToListAsync();

        return Ok(resultados);
    }
}