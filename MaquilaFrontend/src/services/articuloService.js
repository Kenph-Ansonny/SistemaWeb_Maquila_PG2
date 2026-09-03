import api from './api'

export default {
  obtenerTodos() {
    return api.get('/articulos')
  },
  obtenerUnidadesMedida() {
    return api.get('/articulos/unidades-medida')
  },
  crear(payload) {
    return api.post('/articulos', payload)
  },
  actualizar(id, payload) {
    return api.put(`/articulos/${id}`, payload)
  },
  cambiarEstado(id) {
    return api.patch(`/articulos/${id}/toggle-estado`)
  }
}