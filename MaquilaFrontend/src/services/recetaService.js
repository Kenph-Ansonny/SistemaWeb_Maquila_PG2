import api from './api'

const BASE_URL = '/recetas'

export const recetaService = {
  /**
   * Obtiene el catálogo de fichas técnicas (BOM) con filtros opcionales.
   * @param {{ busqueda?: string, mostrarInactivos?: boolean }} filtros
   */
  async obtenerTodos(filtros = {}) {
    const params = {}
    if (filtros.busqueda) params.busqueda = filtros.busqueda.trim()
    if (filtros.mostrarInactivos !== undefined) params.mostrarInactivos = filtros.mostrarInactivos

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

  /**
   * Obtiene el desglose completo de una ficha técnica con su fórmula de insumos y mermas.
   * @param {number|string} id
   */
  async obtenerDetalle(id) {
    const { data } = await api.get(`${BASE_URL}/${id}`)
    return data
  },

  /**
   * Registra una nueva ficha técnica con su lista de materiales y tolerancias de merma.
   * @param {{ nombreReceta: string, idArticuloPrenda: number, descripcionReceta?: string|null, detalles: Array<{ idArticuloInsumo: number, idUnidadConsumo: number, cantidadNeta: number, porcentajeMerma: number }> }} payload
   */
  async crear(payload) {
    const { data } = await api.post(BASE_URL, payload)
    return data
  },

  /**
   * Actualiza una ficha técnica existente y recalcula las cantidades brutas.
   * @param {number|string} id
   * @param {{ nombreReceta: string, idArticuloPrenda: number, descripcionReceta?: string|null, detalles: Array<{ idArticuloInsumo: number, idUnidadConsumo: number, cantidadNeta: number, porcentajeMerma: number }> }} payload
   */
  async actualizar(id, payload) {
    const { data } = await api.put(`${BASE_URL}/${id}`, payload)
    return data
  },

  /**
   * Activa o inhabilita lógicamente una ficha técnica.
   * @param {number|string} id
   */
  async cambiarEstado(id) {
    const { data } = await api.patch(`${BASE_URL}/${id}/toggle-estado`)
    return data
  }
}

export default recetaService