// Utilidades de sesión. Toda la app debe consultar isAuthenticated()
// en vez de leer localStorage directamente, para no duplicar la lógica
// de expiración en cada componente.

export function isAuthenticated() {
    const token = localStorage.getItem('auth_token')
    if (!token) return false
  
    try {
      const payload = JSON.parse(atob(token.split('.')[1]))
      return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now()
    } catch {
      // Token corrupto o mal formado: se trata como no autenticado.
      return false
    }
  }
  
  export function clearSession() {
    // Se eliminan claves puntuales (no localStorage.clear()) para no borrar
    // por accidente otros datos de la app que no sean de sesión.
    localStorage.removeItem('auth_token')
    localStorage.removeItem('user_id')
    localStorage.removeItem('user_name')
    localStorage.removeItem('user_role')
    localStorage.removeItem('user_permissions')
  }