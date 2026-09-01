import axios from 'axios'

// Apunta exactamente al puerto donde está escuchando
const api = axios.create({
  baseURL: 'http://localhost:5220/api',
  headers: {
    'Content-Type': 'application/json'
  }
})

export default api