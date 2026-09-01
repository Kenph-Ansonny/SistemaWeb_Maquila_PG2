import axios from 'axios'

const api = axios.create({
  baseURL: 'http://localhost:5220/api',
  headers: {
    'Content-Type': 'application/json'
  }
})

// Interceptor para inyectar el ID de usuario activo
api.interceptors.request.use((config) => {
  const userId = localStorage.getItem('user_id')
  if (userId) {
    config.headers['X-User-Id'] = userId
  }
  return config
}, (error) => {
  return Promise.reject(error)
})

export default api