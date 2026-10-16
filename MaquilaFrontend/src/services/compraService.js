import api from './api'

const BASE_URL = '/compras'

export const compraService = {
  async obtenerTodas(filtros = {}) {
    const params = {}
    if (filtros.estado) params.estado = filtros.estado
    if (filtros.idProveedor) params.idProveedor = filtros.idProveedor
    if (filtros.fechaDesde) params.fechaDesde = filtros.fechaDesde
    if (filtros.fechaHasta) params.fechaHasta = filtros.fechaHasta
    if (filtros.busqueda) params.busqueda = filtros.busqueda

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

  async obtenerDetalle(id) {
    const { data } = await api.get(`${BASE_URL}/${id}`)
    return data
  },

  async registrar(payload) {
    const { data } = await api.post(BASE_URL, payload)
    return data
  },

  async recibir(id) {
    const { data } = await api.post(`${BASE_URL}/${id}/recibir`)
    return data
  },

  async anular(id) {
    const { data } = await api.patch(`${BASE_URL}/${id}/anular`)
    return data
  }
}

export default compraService