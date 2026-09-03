import api from './api'

export default {
  obtenerTodos() {
    return api.get('/almacenes')
  },
  obtenerInventario(id) {
    return api.get(`/almacenes/${id}/inventario`)
  },
  crear(payload) {
    return api.post('/almacenes', payload)
  },
  actualizar(id, payload) {
    return api.put(`/almacenes/${id}`, payload)
  },
  cambiarEstado(id) {
    return api.patch(`/almacenes/${id}/toggle-estado`)
  }
}