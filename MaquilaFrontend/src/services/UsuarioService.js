import api from './api'

export default {
  obtenerTodos() {
    return api.get('/usuarios')
  },
  obtenerRoles() {
    return api.get('/usuarios/roles-disponibles')
  },
  crear(usuario) {
    return api.post('/usuarios', usuario)
  },
  actualizar(id, usuario) {
    return api.put(`/usuarios/${id}`, usuario)
  },
  cambiarEstado(id) {
    return api.patch(`/usuarios/${id}/toggle-estado`)
  }
}