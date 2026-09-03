import api from './api'

export default {
  obtenerUnidades() {
    return api.get('/unidades')
  },
  crearUnidad(payload) {
    return api.post('/unidades', payload)
  },
  actualizarUnidad(id, payload) {
    return api.put(`/unidades/${id}`, payload)
  },
  obtenerConversiones() {
    return api.get('/unidades/conversiones')
  },
  guardarConversion(payload) {
    return api.post('/unidades/conversiones', payload)
  },
  eliminarConversion(origenId, destinoId) {
    return api.delete(`/unidades/conversiones/${origenId}/${destinoId}`)
  }
}