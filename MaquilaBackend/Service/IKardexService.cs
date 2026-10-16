using MaquilaBackend.Models;

namespace MaquilaBackend.Services;

public interface IKardexService
{
    Task<KardexMovimiento> RegistrarMovimientoAsync(
        int idArticulo,
        int idAlmacen,
        string tipoMovimiento,
        string tipoDocumentoReferencia,
        long idDocumentoReferencia,
        decimal cantidad,
        decimal costoUnitario,
        int idUsuario,
        bool actualizarCostoPromedio = false
    );
}