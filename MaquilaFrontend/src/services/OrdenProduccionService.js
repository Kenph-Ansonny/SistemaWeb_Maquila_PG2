import api from './api'

const BASE_URL = '/OrdenesProduccion'

export const ordenProduccionService = {
  /**
   * Lista paginada de órdenes de producción.
   * @param {{ estado?: string, idPedido?: number, busqueda?: string, fechaDesde?: string,
   *           fechaHasta?: string, pagina?: number, tamanioPagina?: number, ordenAscendente?: boolean }} filtros
   * @returns {Promise<{ items: any[], total: number, pagina: number, tamanioPagina: number, totalPaginas: number }>}
   */
  async obtenerOrdenes(filtros = {}) {
    const params = {}
    if (filtros.estado) params.estado = filtros.estado
    if (filtros.idPedido) params.idPedido = filtros.idPedido
    if (filtros.busqueda) params.busqueda = filtros.busqueda
    if (filtros.fechaDesde) params.fechaDesde = filtros.fechaDesde
    if (filtros.fechaHasta) params.fechaHasta = filtros.fechaHasta
    params.pagina = filtros.pagina || 1
    params.tamanioPagina = filtros.tamanioPagina || 10
    params.ordenAscendente = !!filtros.ordenAscendente

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

  async obtenerDetalle(id) {
    const { data } = await api.get(`${BASE_URL}/${id}`)
    return data
  },

  // Detalles de pedido con saldo pendiente de programar (para el modal "Nueva orden").
  // Cada detalle incluye además los lotes/órdenes que ya existen para esa prenda.
  async obtenerPedidosDisponibles() {
    const { data } = await api.get(`${BASE_URL}/pedidos-disponibles`)
    return data
  },

  // Contexto completo de un pedido (todas sus prendas y lotes), para la vista de avance.
  async obtenerContextoPedido(idPedido) {
    const { data } = await api.get(`${BASE_URL}/contexto-pedido/${idPedido}`)
    return data
  },

  // Crea una orden individual (una sola prenda).
  async crearOrden(payload) {
    const { data } = await api.post(BASE_URL, payload)
    return data
  },

  // Crea varias órdenes juntas (un lote): payload = { fechaInicio, items: [{ idPedidoDetalle, cantidadProgramada }] }
  async crearLote(payload) {
    const { data } = await api.post(`${BASE_URL}/lote`, payload)
    return data
  },

  // payload: { estadoOrden, cantidadProducida }
  async registrarAvance(id, payload) {
    const { data } = await api.put(`${BASE_URL}/${id}/avance`, payload)
    return data
  },

  // Cancela una orden (conserva el registro, a diferencia de eliminar).
  async cancelarOrden(id) {
    const { data } = await api.patch(`${BASE_URL}/${id}/cancelar`)
    return data
  },

  async eliminarOrden(id) {
    const { data } = await api.delete(`${BASE_URL}/${id}`)
    return data
  }
}

export default ordenProduccionService