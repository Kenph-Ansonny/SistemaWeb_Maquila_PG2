import api from './api'

const BASE_URL = '/unidades'

export const unidadService = {
  /**
   * Obtiene el catálogo de unidades de medida con el conteo de artículos asociados.
   */
  async obtenerUnidades() {
    const { data } = await api.get(BASE_URL)
    return data
  },

  /**
   * Registra una nueva unidad de medida en el sistema.
   * @param {{ codigoMedida: string, nombreMedida: string, tipoMedida: string }} payload
   */
  async crearUnidad(payload) {
    const { data } = await api.post(BASE_URL, payload)
    return data
  },

  /**
   * Actualiza el código, nombre o dimensión de una unidad de medida existente.
   * @param {number|string} id
   * @param {{ codigoMedida: string, nombreMedida: string, tipoMedida: string }} payload
   */
  async actualizarUnidad(id, payload) {
    const { data } = await api.put(`${BASE_URL}/${id}`, payload)
    return data
  },

  /**
   * Obtiene la matriz de factores de conversión directa entre unidades.
   */
  async obtenerConversiones() {
    const { data } = await api.get(`${BASE_URL}/conversiones`)
    return data
  },

  /**
   * Guarda o actualiza un factor de conversión y calcula automáticamente su recíproco.
   * @param {{ idUnidadOrigen: number, idUnidadDestino: number, factorConversion: number }} payload
   */
  async guardarConversion(payload) {
    const { data } = await api.post(`${BASE_URL}/conversiones`, payload)
    return data
  },

  /**
   * Elimina una regla de conversión bidireccional entre dos unidades.
   * @param {number|string} origenId
   * @param {number|string} destinoId
   */
  async eliminarConversion(origenId, destinoId) {
    const { data } = await api.delete(`${BASE_URL}/conversiones/${origenId}/${destinoId}`)
    return data
  }
}

export default unidadService