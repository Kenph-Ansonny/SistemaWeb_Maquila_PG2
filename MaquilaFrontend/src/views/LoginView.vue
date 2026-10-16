<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import api from '../services/api'
import maquilaImg from '../assets/Login_FondoMaquila.jpg'
import logoImg from '../assets/logo.png'

const router = useRouter()
const route = useRoute()
const identifier = ref('')
const password = ref('')
const showPassword = ref(false)
const rememberMe = ref(false)
const isLoading = ref(false)

// Bloqueo temporal por fuerza bruta
const countdownSegundos = ref(0)
let timerInterval = null

// Notificación flotante
const notification = ref({
  show: false,
  message: '',
  type: 'error'
})

const triggerNotification = (message, type = 'error') => {
  notification.value = { show: true, message, type }
  setTimeout(() => {
    notification.value.show = false
  }, 4500)
}

const iniciarContadorBloqueo = (segundos) => {
  countdownSegundos.value = segundos
  clearInterval(timerInterval)
  timerInterval = setInterval(() => {
    if (countdownSegundos.value > 1) {
      countdownSegundos.value--
    } else {
      countdownSegundos.value = 0
      clearInterval(timerInterval)
      triggerNotification('El tiempo de espera ha finalizado. Puedes volver a intentar.', 'success')
    }
  }, 1000)
}

onMounted(() => {
  if (route.query.sessionExpired) {
    triggerNotification('Tu sesión finalizó o no has iniciado sesión. Ingresa nuevamente.', 'error')
    // Limpia inmediatamente el parámetro de la URL para que no vuelva a saltar en reintentos
    router.replace({ query: {} })
  }
})

onUnmounted(() => {
  clearInterval(timerInterval)
})

const handleLogin = async () => {
  if (countdownSegundos.value > 0) {
    triggerNotification(`Cuenta bloqueada temporalmente. Espera ${countdownSegundos.value}s.`, 'error')
    return
  }

  if (!identifier.value || !password.value) {
    triggerNotification('Ingresa tu usuario/correo y contraseña.', 'error')
    return
  }

  isLoading.value = true

  try {
    const res = await api.post('/auth/login', {
      identificador: identifier.value,
      password: password.value,
      recordarme: rememberMe.value
    })

    const { idUsuario, nombreUsuario, rolPrincipal, permisos, token } = res.data

    localStorage.setItem('auth_token', token)
    localStorage.setItem('user_id', String(idUsuario))
    localStorage.setItem('user_name', nombreUsuario)
    localStorage.setItem('user_role', rolPrincipal)
    localStorage.setItem('user_permissions', JSON.stringify(permisos || []))

    triggerNotification(`¡Bienvenido/a, ${nombreUsuario}!`, 'success')

    setTimeout(() => {
      isLoading.value = false
      router.push('/dashboard')
    }, 1200)

  } catch (error) {
    isLoading.value = false
    const status = error.response?.status
    const data = error.response?.data
    const msg = data?.message || 'Error de conexión con el servidor.'

    if (status === 429 && data?.segundosRestantes) {
      iniciarContadorBloqueo(data.segundosRestantes)
    }

    triggerNotification(msg, 'error')
  }
}
</script>

<template>
  <div class="relative flex min-h-screen bg-white">
    <!-- Toast Flotante -->
    <transition
      enter-active-class="transform ease-out duration-300 transition"
      enter-from-class="translate-y-[-20px] opacity-0"
      enter-to-class="translate-y-0 opacity-100"
      leave-active-class="transition ease-in duration-200"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div 
        v-if="notification.show" 
        :class="[
          'fixed top-5 right-5 z-50 flex items-center px-4 py-3 rounded-2xl shadow-xl text-sm font-semibold border backdrop-blur-md transition-all',
          notification.type === 'success' 
            ? 'bg-emerald-500/90 text-white border-emerald-400' 
            : 'bg-rose-500/90 text-white border-rose-400'
        ]"
      >
        <svg v-if="notification.type === 'success'" class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
        </svg>
        <svg v-else class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <span>{{ notification.message }}</span>
      </div>
    </transition>

    <!-- Panel Izquierdo: Formulario -->
    <div class="flex flex-1 flex-col justify-center px-6 py-12 sm:px-12 lg:flex-none lg:w-1/2 xl:w-5/12">
      <div class="mx-auto w-full max-w-sm lg:w-96">
        <div>
          <div class="flex items-center">
            <img :src="logoImg" alt="Logo Maquila" class="h-12 w-auto object-contain rounded-lg" />
          </div>
          <h2 class="mt-6 text-3xl font-extrabold tracking-tight text-slate-900">Inicio de Sesión</h2>
          <p class="mt-2 text-sm text-slate-600">
            Ingresa tus credenciales para acceder al sistema.
          </p>
        </div>

        <!-- Banner de Bloqueo Progresivo -->
        <div v-if="countdownSegundos > 0" class="mt-4 p-3.5 bg-rose-50 border border-rose-200 rounded-xl flex items-center gap-3">
          <div class="w-8 h-8 rounded-lg bg-rose-100 text-rose-600 flex items-center justify-center font-bold text-xs shrink-0">
            ⏱
          </div>
          <div class="text-xs">
            <span class="font-bold text-rose-800 block">5 intentos alcanzados</span>
            <span class="text-rose-600 font-medium">Reintenta en: <b class="font-mono">{{ countdownSegundos }}s</b></span>
          </div>
        </div>

        <div class="mt-6">
          <form @submit.prevent="handleLogin" class="space-y-5">
            <div>
              <label for="identifier" class="block text-sm font-medium text-slate-700">Usuario o Correo Electrónico</label>
              <div class="mt-1">
                <input
                  id="identifier"
                  v-model="identifier"
                  type="text"
                  required
                  :disabled="countdownSegundos > 0"
                  placeholder="admin o admin@maquila.com"
                  class="block w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-slate-900 placeholder-slate-400 shadow-sm focus:border-indigo-600 focus:outline-none focus:ring-2 focus:ring-indigo-600/20 sm:text-sm disabled:opacity-50"
                />
              </div>
            </div>

            <div>
              <div class="flex items-center justify-between">
                <label for="password" class="block text-sm font-medium text-slate-700">Contraseña</label>
                <label class="flex items-center gap-1.5 text-xs text-slate-500 cursor-pointer select-none">
                  <input 
                    type="checkbox" 
                    v-model="showPassword" 
                    class="h-3.5 w-3.5 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <span>Mostrar</span>
                </label>
              </div>
              <div class="mt-1">
                <input
                  id="password"
                  v-model="password"
                  :type="showPassword ? 'text' : 'password'"
                  required
                  :disabled="countdownSegundos > 0"
                  placeholder="••••••••"
                  class="block w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-slate-900 placeholder-slate-400 shadow-sm focus:border-indigo-600 focus:outline-none focus:ring-2 focus:ring-indigo-600/20 sm:text-sm disabled:opacity-50"
                />
              </div>
            </div>

            <div class="flex items-center justify-between">
              <div class="flex items-center">
                <input
                  id="remember-me"
                  v-model="rememberMe"
                  type="checkbox"
                  class="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
                />
                <label for="remember-me" class="ml-2 block text-sm text-slate-700 select-none">Recordarme</label>
              </div>
            </div>

            <div>
              <button
                type="submit"
                :disabled="isLoading || countdownSegundos > 0"
                class="flex w-full justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 transition duration-150 disabled:opacity-50"
              >
                <span v-if="countdownSegundos > 0">Bloqueado temporalmente ({{ countdownSegundos }}s)</span>
                <span v-else-if="!isLoading">Iniciar Sesión</span>
                <span v-else>Verificando credenciales...</span>
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Panel Derecho -->
    <div class="relative hidden w-0 flex-1 lg:block">
      <img
        class="absolute inset-0 h-full w-full object-cover"
        :src="maquilaImg"
        alt="Rollos de tela - Maquila"
      />
      <div class="absolute inset-0 bg-slate-900/20"></div>
    </div>
  </div>
</template>