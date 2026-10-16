import api from './api'

const BASE_URL = '/proveedores'

export const proveedorService = {
  async obtenerTodos(filtros = {}) {
    const params = {}
    if (filtros.soloActivos !== undefined && filtros.soloActivos !== '') params.soloActivos = filtros.soloActivos
    if (filtros.busqueda) params.busqueda = filtros.busqueda

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

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
    const { data } = await api.patch(`${BASE_URL}/${id}/estado`)
    return data
  }
}

export default proveedorService