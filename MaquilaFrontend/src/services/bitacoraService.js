import api from './api'

export default {
  obtenerRegistros(filtros = {}) {
    return api.get('/bitacora', { params: filtros })
  }
}