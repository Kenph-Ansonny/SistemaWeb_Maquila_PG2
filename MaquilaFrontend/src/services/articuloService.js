import api from './api'

const BASE_URL = '/articulos'

export const articuloService = {
  /**
   * Obtiene el catálogo de artículos con existencias consolidadas y filtros opcionales.
   * @param {{ busqueda?: string, tipo?: string, mostrarInactivos?: boolean }} filtros
   */
  async obtenerTodos(filtros = {}) {
    const params = {}
    if (filtros.busqueda) params.busqueda = filtros.busqueda.trim()
    if (filtros.tipo) params.tipo = filtros.tipo
    if (filtros.mostrarInactivos !== undefined) params.mostrarInactivos = filtros.mostrarInactivos

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

  /**
   * Obtiene la lista de unidades de medida para selects.
   */
  async obtenerUnidadesMedida() {
    const { data } = await api.get(`${BASE_URL}/unidades-medida`)
    return data
  },

  /**
   * Registra un nuevo artículo o tela en el catálogo maestro.
   * @param {{ codigoArticulo: string, nombreArticulo: string, tipoArticulo: string, idUnidadBaseMedida: number, stockMinimo: number, costoPromedio: number, talla?: string|null, color?: string|null }} payload
   */
  async crear(payload) {
    const { data } = await api.post(BASE_URL, payload)
    return data
  },

  /**
   * Actualiza las especificaciones de un artículo existente.
   * @param {number|string} id
   * @param {{ codigoArticulo: string, nombreArticulo: string, tipoArticulo: string, idUnidadBaseMedida: number, stockMinimo: number, costoPromedio: number, talla?: string|null, color?: string|null }} payload
   */
  async actualizar(id, payload) {
    const { data } = await api.put(`${BASE_URL}/${id}`, payload)
    return data
  },

  /**
   * Activa o desactiva lógicamente un artículo.
   * @param {number|string} id
   */
  async cambiarEstado(id) {
    const { data } = await api.patch(`${BASE_URL}/${id}/toggle-estado`)
    return data
  }
}

export default articuloService