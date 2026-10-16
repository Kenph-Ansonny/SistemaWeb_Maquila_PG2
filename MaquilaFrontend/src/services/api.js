import axios from 'axios'
import router from '../router'
import { clearSession } from '../utils/auth' // o de donde tengas la limpieza de sesión

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5220/api',
  headers: {
    'Content-Type': 'application/json'
  }
})

// adjunta token y usuario
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('auth_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    const userId = localStorage.getItem('user_id')
    if (userId) {
      config.headers['X-User-Id'] = userId
    }
    return config
  },
  (error) => Promise.reject(error)
)

//captura sesiones vencidas sin interferir con el Login
api.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status
    const url = error.config?.url || ''

    const esPeticionLogin = url.includes('/auth/login')

    if (status === 401 && !esPeticionLogin) {
      clearSession()
      if (router.currentRoute.value.path !== '/') {
        router.push({ path: '/', query: { sessionExpired: 'true' } })
      }
    }

    return Promise.reject(error)
  }
)

export default api