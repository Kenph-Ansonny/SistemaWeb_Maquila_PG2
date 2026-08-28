<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import maquilaImg from '../assets/Login_FondoMaquila.jpg'

const router = useRouter()
const username = ref('')
const password = ref('')
const rememberMe = ref(false)
const errorMessage = ref('')
const isLoading = ref(false)

const handleLogin = async () => {
  errorMessage.value = ''
  isLoading.value = true

  // Validación rápida local
  if (!username.value || !password.value) {
    errorMessage.value = 'Por favor ingresa usuario y contraseña.'
    isLoading.value = false
    return
  }

  try {
    // Aquí se conectará la llamada a .NET Core API
    setTimeout(() => {
      isLoading.value = false
      router.push('/dashboard')
    }, 600)
  } catch (error) {
    isLoading.value = false
    errorMessage.value = 'Credenciales inválidas. Verifica tus datos.'
  }
}
</script>

<template>
  <div class="flex min-h-screen bg-white">
    <!-- Panel Izquierdo: Formulario -->
    <div class="flex flex-1 flex-col justify-center px-6 py-12 sm:px-12 lg:flex-none lg:w-1/2 xl:w-5/12">
      <div class="mx-auto w-full max-w-sm lg:w-96">
        <div>
          <!-- Icono / Logo Maquila -->
          <div class="flex h-12 w-12 items-center justify-center rounded-xl bg-indigo-600 text-white shadow-md">
            <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          </div>
          <h2 class="mt-6 text-3xl font-extrabold tracking-tight text-slate-900"> Inicio de Sesión </h2>
          <p class="mt-2 text-sm text-slate-600">
            Ingresa tus credenciales para acceder al sistema.
          </p>
        </div>

        <div class="mt-8">
          <!-- Alerta de error -->
          <div v-if="errorMessage" class="mb-4 rounded-lg bg-red-50 p-3 text-sm text-red-600 border border-red-200">
            {{ errorMessage }}
          </div>

          <form @submit.prevent="handleLogin" class="space-y-5">
            <div>
              <label for="username" class="block text-sm font-medium text-slate-700">Usuario o Correo</label>
              <div class="mt-1">
                <input
                  id="username"
                  v-model="username"
                  type="text"
                  required
                  placeholder="usuario@maquila.com"
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
                <a href="#" class="font-medium text-indigo-600 hover:text-indigo-500">¿Olvidaste tu contraseña?</a>
              </div>
            </div>

            <div>
              <button
                type="submit"
                :disabled="isLoading"
                class="flex w-full justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 transition duration-150 disabled:opacity-50"
              >
                <span v-if="!isLoading">Iniciar Sesión</span>
                <span v-else>Iniciando...</span>
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Panel Derecho: Imagen Responsiva -->
    <div class="relative hidden w-0 flex-1 lg:block">
      <!-- Ruta scr de la imagen -->
      <img
        class="absolute inset-0 h-full w-full object-cover"
        :src="maquilaImg"
        alt="Rollos de tela - Maquila"
      />
      <!-- Capa de oscurecimiento suave para balance visual -->
      <div class="absolute inset-0 bg-slate-900/20"></div>
    </div>
  </div>
</template>