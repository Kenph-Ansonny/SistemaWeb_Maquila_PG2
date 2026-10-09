import api from './api'

const BASE_URL = '/clientes'

export const clienteService = {
  /**
   * Obtiene la lista completa de clientes con filtros opcionales.
   * @param {{ busqueda?: string, soloActivos?: boolean }} filtros
   */
  async obtenerTodos(filtros = {}) {
    const params = {}
    if (filtros.busqueda) params.busqueda = filtros.busqueda
    if (filtros.soloActivos !== undefined) params.soloActivos = filtros.soloActivos

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

  /**
   * Obtiene lista simplificada de clientes activos para comboboxes/selects.
   */
  async obtenerOpciones() {
    const { data } = await api.get(`${BASE_URL}/opciones`)
    return data
  },

  async obtenerPorId(id) {
    const { data } = await api.get(`${BASE_URL}/${id}`)
    return data
  },

  async crear(payload) {
    const { data } = await api.post(BASE_URL, payload)
    return data
  },

  async actualizar(id, payload) {
    const { data } = await api.put(`${BASE_URL}/${id}`, payload)
    return data
  },

  async cambiarEstado(id) {
    const { data } = await api.patch(`${BASE_URL}/${id}/toggle-estado`)
    return data
  }
}

export default clienteService