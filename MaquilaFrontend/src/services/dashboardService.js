import api from './api'

const BASE_URL = '/dashboard'

export const dashboardService = {
  /**
   * Obtiene el resumen de KPIs, histórico de ingresos y embudo de la maquila
   * @param {'semana'|'mes'|'anio'} periodo
   */
  async obtenerResumen(periodo = 'mes') {
    const { data } = await api.get(`${BASE_URL}/resumen`, { params: { periodo } })
    return data
  }
}

export default dashboardService