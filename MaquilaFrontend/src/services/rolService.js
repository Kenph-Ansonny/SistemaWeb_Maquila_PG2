import api from './api'

export default {
  obtenerRoles() {
    return api.get('/roles')
  },
  obtenerPermisos(idRol) {
    return api.get(`/roles/${idRol}/permisos`)
  },
  crear(rol) {
    return api.post('/roles', rol)
  },
  actualizar(idRol, rol) {
    return api.put(`/roles/${idRol}`, rol)
  },
  guardarPermisos(idRol, permisos) {
    return api.put(`/roles/${idRol}/permisos`, permisos)
  },
  cambiarEstado(idRol) {
    return api.patch(`/roles/${idRol}/toggle-estado`)
  }
}