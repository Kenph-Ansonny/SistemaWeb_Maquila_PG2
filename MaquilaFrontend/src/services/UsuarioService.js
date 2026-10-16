import api from './api'

const BASE_URL = '/usuarios'

export const usuarioService = {
  /**
   * Obtiene la lista completa de usuarios con roles y fechas de acceso.
   */
  async obtenerTodos() {
    const { data } = await api.get(BASE_URL)
    return data
  },

  /**
   * Obtiene el catálogo de roles activos disponibles para asignación.
   */
  async obtenerRoles() {
    const { data } = await api.get(`${BASE_URL}/roles-disponibles`)
    return data
  },

  /**
   * Registra un nuevo usuario con contraseña hasheada y roles asignados.
   * @param {{ nombreUsuario: string, correo: string, password: string, rolesIds: number[] }} usuario
   */
  async crear(usuario) {
    const { data } = await api.post(BASE_URL, usuario)
    return data
  },

  /**
   * Actualiza la información, roles y opcionalmente la contraseña de un usuario.
   * @param {number|string} id
   * @param {{ nombreUsuario: string, correo: string, password?: string, rolesIds: number[] }} usuario
   */
  async actualizar(id, usuario) {
    const { data } = await api.put(`${BASE_URL}/${id}`, usuario)
    return data
  },

  /**
   * Alterna el estado activo o inactivo (y bloqueo) de una cuenta.
   * @param {number|string} id
   */
  async cambiarEstado(id) {
    const { data } = await api.patch(`${BASE_URL}/${id}/toggle-estado`)
    return data
  }
}

export default usuarioService