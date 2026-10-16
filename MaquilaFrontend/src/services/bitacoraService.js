import api from './api'

const BASE_URL = '/bitacora'

export const bitacoraService = {
  /**
   * Obtiene los registros de auditoría y trazabilidad del sistema con filtros avanzados.
   * @param {{ busqueda?: string, modulo?: string, accion?: string, fechaInicio?: string, fechaFin?: string }} filtros
   */
  async obtenerRegistros(filtros = {}) {
    const params = {}
    if (filtros.busqueda) params.busqueda = filtros.busqueda.trim()
    if (filtros.modulo) params.modulo = filtros.modulo
    if (filtros.accion) params.accion = filtros.accion
    if (filtros.fechaInicio) params.fechaInicio = filtros.fechaInicio
    if (filtros.fechaFin) params.fechaFin = filtros.fechaFin

    const { data } = await api.get(BASE_URL, { params })
    return data
  }
}

export default bitacoraService