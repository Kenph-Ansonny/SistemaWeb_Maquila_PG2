// Todo se autentica aquí

export function isAuthenticated() {
    const token = localStorage.getItem('auth_token')
    if (!token) return false
  
    try {
      const payload = JSON.parse(atob(token.split('.')[1]))
      return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now()
    } catch {
      // Token corrupto o mal formado es tratdoa como no autenticado.
      return false
    }
  }
  
  export function clearSession() {
    localStorage.removeItem('auth_token')
    localStorage.removeItem('user_id')
    localStorage.removeItem('user_name')
    localStorage.removeItem('user_role')
    localStorage.removeItem('user_permissions')
  }