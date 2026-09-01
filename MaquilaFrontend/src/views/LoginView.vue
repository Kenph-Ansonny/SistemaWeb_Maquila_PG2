<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../services/api'
import maquilaImg from '../assets/Login_FondoMaquila.jpg'
import logoImg from '../assets/logo.png'

const router = useRouter()
const identifier = ref('')
const password = ref('')
const rememberMe = ref(false)
const isLoading = ref(false)

// Estados para notificaciones interactivas
const notification = ref({
  show: false,
  message: '',
  type: 'error' // 'success' o 'error'
})

const triggerNotification = (message, type = 'error') => {
  notification.value = { show: true, message, type }
  setTimeout(() => {
    notification.value.show = false
  }, 4000)
}

const handleLogin = async () => {
  if (!identifier.value || !password.value) {
    triggerNotification('Ingresa tu usuario/correo y contraseña.', 'error')
    return
  }

  isLoading.value = true

  try {
    const res = await api.post('/auth/login', {
      identificador: identifier.value,
      password: password.value
    })

    const { nombreUsuario, rolPrincipal } = res.data

    //guarda sesion
    localStorage.setItem('user_name', nombreUsuario)
    localStorage.setItem('user_role', rolPrincipal)

    triggerNotification(`¡Bienvenido/a, ${nombreUsuario}!`, 'success')

    setTimeout(() => {
      isLoading.value = false
      router.push('/dashboard')
    }, 1200)

  } catch (error) {
    isLoading.value = false
    const msg = error.response?.data?.message || 'Error de conexión con el servidor.'
    triggerNotification(msg, 'error')
  }
}
</script>

<template>
  <div class="relative flex min-h-screen bg-white">
    <!-- Toast / Notificación Flotante Interactiva -->
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
        <!-- Icono Éxito -->
        <svg v-if="notification.type === 'success'" class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
        </svg>
        <!-- Icono Error -->
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

        <div class="mt-8">
          <form @submit.prevent="handleLogin" class="space-y-5">
            <div>
              <label for="identifier" class="block text-sm font-medium text-slate-700">Usuario o Correo Electrónico</label>
              <div class="mt-1">
                <input
                  id="identifier"
                  v-model="identifier"
                  type="text"
                  required
                  placeholder="admin o admin@maquila.com"
                  class="block w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-slate-900 placeholder-slate-400 shadow-sm focus:border-indigo-600 focus:outline-none focus:ring-2 focus:ring-indigo-600/20 sm:text-sm"
                />
              </div>
            </div>
            <div>
              <label for="password" class="block text-sm font-medium text-slate-700">Contraseña</label>
              <div class="mt-1">
                <input
                  id="password"
                  v-model="password"
                  type="password"
                  required
                  placeholder="••••••••"
                  class="block w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-slate-900 placeholder-slate-400 shadow-sm focus:border-indigo-600 focus:outline-none focus:ring-2 focus:ring-indigo-600/20 sm:text-sm"
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
                <label for="remember-me" class="ml-2 block text-sm text-slate-700">Recordarme</label>
              </div>

              <div class="text-sm">
                <router-link to="/reset-password" class="font-medium text-indigo-600 hover:text-indigo-500">
                  ¿Olvidaste tu contraseña?
                </router-link>
              </div>
            </div>

            <div>
              <button
                type="submit"
                :disabled="isLoading"
                class="flex w-full justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 transition duration-150 disabled:opacity-50"
              >
                <span v-if="!isLoading">Iniciar Sesión</span>
                <span v-else>Verificando credenciales...</span>
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Panel Derecho: Imagen Responsiva -->
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