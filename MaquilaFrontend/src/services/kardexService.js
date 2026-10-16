import api from './api'

const BASE_URL = '/kardex'

export const kardexService = {
  /**
   * Obtiene el libro mayor de movimientos con filtros opcionales.
   */
  async obtenerMovimientos(filtros = {}) {
    const params = {}
    if (filtros.idArticulo) params.idArticulo = filtros.idArticulo
    if (filtros.idAlmacen) params.idAlmacen = filtros.idAlmacen
    if (filtros.tipoMovimiento) params.tipoMovimiento = filtros.tipoMovimiento
    if (filtros.fechaDesde) params.fechaDesde = filtros.fechaDesde
    if (filtros.fechaHasta) params.fechaHasta = filtros.fechaHasta
    if (filtros.busqueda) params.busqueda = filtros.busqueda

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

  /**
   * Consulta existencias consolidadas por artículo y desglose por bodega.
   */
  async obtenerExistencias(tipoArticulo = null) {
    const params = {}
    if (tipoArticulo) params.tipoArticulo = tipoArticulo
    const { data } = await api.get(`${BASE_URL}/articulos/existencias`, { params })
    return data
  },

  /**
   * Registra un ajuste manual de inventario (+ o -).
   */
  async registrarAjuste(payload) {
    const { data } = await api.post(`${BASE_URL}/ajuste`, payload)
    return data
  }
}

export default kardexService