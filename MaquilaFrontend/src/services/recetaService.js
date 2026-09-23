import api from './api'

export default {
  obtenerTodos() {
    return api.get('/recetas')
  },
  obtenerDetalle(id) {
    return api.get(`/recetas/${id}`)
  },
  crear(payload) {
    return api.post('/recetas', payload)
  },
  actualizar(id, payload) {
    return api.put(`/recetas/${id}`, payload)
  },
  cambiarEstado(id) {
    return api.patch(`/recetas/${id}/toggle-estado`)
  }
}