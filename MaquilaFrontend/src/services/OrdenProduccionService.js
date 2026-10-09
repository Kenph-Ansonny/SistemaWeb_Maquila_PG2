import api from './api'

const BASE_URL = '/OrdenesProduccion'

export const ordenProduccionService = {
  /**
   * Lista órdenes de producción con filtros opcionales.
   * @param {{ estado?: string, idPedido?: number, busqueda?: string }} filtros
   */
  async obtenerOrdenes(filtros = {}) {
    const params = {}
    if (filtros.estado) params.estado = filtros.estado
    if (filtros.idPedido) params.idPedido = filtros.idPedido
    if (filtros.busqueda) params.busqueda = filtros.busqueda

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

  async obtenerDetalle(id) {
    const { data } = await api.get(`${BASE_URL}/${id}`)
    return data
  },

  // Detalles de pedido con saldo pendiente de programar
  async obtenerPedidosDisponibles() {
    const { data } = await api.get(`${BASE_URL}/pedidos-disponibles`)
    return data
  },

  // Contexto completo de un pedido
  async obtenerContextoPedido(idPedido) {
    const { data } = await api.get(`${BASE_URL}/contexto-pedido/${idPedido}`)
    return data
  },

  async crearOrden(payload) {
    const { data } = await api.post(BASE_URL, payload)
    return data
  },

  async registrarAvance(id, payload) {
    const { data } = await api.put(`${BASE_URL}/${id}/avance`, payload)
    return data
  },

  async eliminarOrden(id) {
    const { data } = await api.delete(`${BASE_URL}/${id}`)
    return data
  }
}

export default ordenProduccionService