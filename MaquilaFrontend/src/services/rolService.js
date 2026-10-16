import api from './api'

const BASE_URL = '/roles'

export const rolService = {
  /**
   * Obtiene todos los roles registrados en el sistema junto con la cantidad de usuarios asignados.
   */
  async obtenerRoles() {
    const { data } = await api.get(BASE_URL)
    return data
  },

  /**
   * Obtiene la matriz de permisos de todos los módulos para un rol específico.
   * @param {number|string} idRol
   */
  async obtenerPermisos(idRol) {
    const { data } = await api.get(`${BASE_URL}/${idRol}/permisos`)
    return data
  },

  /**
   * Registra un nuevo rol en el catálogo del sistema.
   * @param {{ nombreRol: string, descripcion?: string|null }} rol
   */
  async crear(rol) {
    const { data } = await api.post(BASE_URL, rol)
    return data
  },

  /**
   * Actualiza el nombre o la descripción de un rol existente.
   * @param {number|string} idRol
   * @param {{ nombreRol: string, descripcion?: string|null }} rol
   */
  async actualizar(idRol, rol) {
    const { data } = await api.put(`${BASE_URL}/${idRol}`, rol)
    return data
  },

  /**
   * Actualiza la matriz granular de privilegios asignados al rol.
   * @param {number|string} idRol
   * @param {Array<{ idModulo: number, puedeConsultar: boolean, puedeInsertar: boolean, puedeModificar: boolean, puedeEliminar: boolean }>} permisos
   */
  async guardarPermisos(idRol, permisos) {
    const { data } = await api.put(`${BASE_URL}/${idRol}/permisos`, permisos)
    return data
  },

  /**
   * Alterna el estado activo o inactivo de un rol.
   * @param {number|string} idRol
   */
  async cambiarEstado(idRol) {
    const { data } = await api.patch(`${BASE_URL}/${idRol}/toggle-estado`)
    return data
  }
}

export default rolService