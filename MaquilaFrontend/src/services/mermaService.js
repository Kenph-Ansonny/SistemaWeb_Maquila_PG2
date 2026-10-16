import api from './api'

const BASE_URL = '/mermas'

export const mermaService = {
  /**
   * Obtiene la lista de mermas con filtros opcionales.
   * @param {{ idOrden?: number, tipoMerma?: string, busqueda?: string }} filtros
   */
  async obtenerTodos(filtros = {}) {
    const params = {}
    if (filtros.idOrden) params.idOrden = filtros.idOrden
    if (filtros.tipoMerma) params.tipoMerma = filtros.tipoMerma
    if (filtros.busqueda) params.busqueda = filtros.busqueda

    const { data } = await api.get(BASE_URL, { params })
    return data
  },

  /**
   * Carga los insumos que consumió la orden junto a la merma teórica calculada por la receta.
   * @param {number} idOrden
   */
  async obtenerInsumosDeOrden(idOrden) {
    const { data } = await api.get(`${BASE_URL}/orden/${idOrden}/insumos`)
    return data
  },

  /**
   * Registra una nueva merma física real.
   * @param {{ idOrdenProduccion: number, idArticuloInsumo: number, cantidadMermaReal: number, tipoMerma: string, idAlmacenDestino?: number|null, observaciones?: string|null }} payload
   */
  async crear(payload) {
    const { data } = await api.post(BASE_URL, payload)
    return data
  },

  /**
   * Anula un registro de merma y revierte el stock si era retazo aprovechable.
   * @param {number|string} id
   */
  async eliminar(id) {
    const { data } = await api.delete(`${BASE_URL}/${id}`)
    return data
  }
}

export default mermaService