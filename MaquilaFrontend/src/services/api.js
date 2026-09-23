import axios from 'axios'
import { clearSession } from '../utils/auth'

const api = axios.create({
  baseURL: 'http://localhost:5220/api',
  headers: {
    'Content-Type': 'application/json'
  }
})

// Interceptor de petición adjunta el token JWT a cada llamada
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// respuesta, si el backend responde 401 (token vencido, inválido o ausente), se limpia la sesión y se regresa al login.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      clearSession()
      window.location.href = '/?sessionExpired=1'
    }
    return Promise.reject(error)
  }
)

export default api