namespace MaquilaBackend.DTOs;

public record DashboardKpisDto(
    decimal TotalIngresosPeriodo,
    int TotalPedidosPeriodo,
    int TotalPrendasProgramadas,
    int OrdenesEnTaller
);

public record IngresoPeriodoPuntoDto(
    string Etiqueta,
    decimal Ingresos,
    int CantidadPedidos,
    int TotalPrendas
);

public record EmbudoEtapaDto(
    string Etapa,
    int TotalOrdenes,
    int TotalPrendas,
    decimal PorcentajeDelTotal
);

public record DashboardResumenDto(
    DashboardKpisDto Kpis,
    List<IngresoPeriodoPuntoDto> GraficoIngresos,
    List<EmbudoEtapaDto> EmbudoProduccion
);