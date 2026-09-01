import { ref } from 'vue'

export function usePermissions() {
  const getPermissions = () => {
    try {
      return JSON.parse(localStorage.getItem('user_permissions') || '[]')
    } catch {
      return []
    }
  }

  const can = (moduleCode, action = 'consultar') => {
    const role = localStorage.getItem('user_role')
    // El Administrador mantiene acceso global
    if (role === 'Administrador' || role === 'admin') return true

    const permissions = getPermissions()
    const mod = permissions.find(p => p.codigoModulo?.toUpperCase() === moduleCode?.toUpperCase())
    if (!mod) return false

    switch (action.toLowerCase()) {
      case 'consultar': return Boolean(mod.puedeConsultar)
      case 'insertar': return Boolean(mod.puedeInsertar)
      case 'modificar': return Boolean(mod.puedeModificar)
      case 'eliminar':
      case 'desactivar': return Boolean(mod.puedeEliminar)
      default: return false
    }
  }

  // Comprueba si al menos un submódulo de una categoría es visible
  const canAny = (moduleCodes = []) => {
    const role = localStorage.getItem('user_role')
    if (role === 'Administrador' || role === 'admin') return true
    return moduleCodes.some(code => can(code, 'consultar'))
  }

  return { can, canAny }
}