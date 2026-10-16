import api from './api'

const BASE_URL = '/Pedidos'

const pedidoService = {
  /**
   * @param {{ estado?: string, idCliente?: number, busqueda?: string }} params
   */
  obtenerTodos(params = {}) {
    return api.get(BASE_URL, { params })
  },

  obtenerDetalle(id) {
    return api.get(`${BASE_URL}/${id}`)
  },

  crear(payload) {
    return api.post(BASE_URL, payload)
  },

  actualizar(id, payload) {
    return api.put(`${BASE_URL}/${id}`, payload)
  },

  cambiarEstado(id, estadoPedido) {
    return api.patch(`${BASE_URL}/${id}/estado`, { estadoPedido })
  },

  actualizarFechaEntrega(id, fechaEstimadaEntrega) {
    return api.patch(`${BASE_URL}/${id}/fecha-entrega`, { fechaEstimadaEntrega })
  },

  eliminar(id) {
    return api.delete(`${BASE_URL}/${id}`)
  },

  obtenerSiguienteNumero() {
    return api.get(`${BASE_URL}/siguiente-numero`)
  }
}

export default pedidoService