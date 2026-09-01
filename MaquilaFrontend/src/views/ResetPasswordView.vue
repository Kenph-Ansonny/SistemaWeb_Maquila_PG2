<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import axios from 'axios'

const route = useRoute()
const router = useRouter()

const token = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const errorMessage = ref('')
const successMessage = ref('')

onMounted(() => {
  token.value = route.query.token || ''
  if (!token.value) {
    errorMessage.value = 'Token de recuperación no encontrado o inválido.'
  }
})

const handleReset = async () => {
  if (newPassword.value !== confirmPassword.value) {
    errorMessage.value = 'Las contraseñas no coinciden.'
    return
  }

  try {
    await axios.post('/api/auth/reset-password', {
      token: token.value,
      newPassword: newPassword.value
    })
    successMessage.value = 'Contraseña actualizada. Redirigiendo al login...'
    setTimeout(() => router.push('/'), 2000)
  } catch (error) {
    errorMessage.value = error.response?.data?.message || 'Error al restablecer la contraseña.'
  }
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-slate-100 px-4">
    <div class="max-w-md w-full bg-white p-8 rounded-2xl shadow-lg border border-slate-100">
      <h2 class="text-xl font-bold text-slate-800 text-center mb-4">Restablecer Contraseña</h2>
      
      <div v-if="errorMessage" class="mb-4 p-3 bg-red-50 text-red-600 text-sm rounded-xl">
        {{ errorMessage }}
      </div>
      <div v-if="successMessage" class="mb-4 p-3 bg-emerald-50 text-emerald-600 text-sm rounded-xl">
        {{ successMessage }}
      </div>

      <form v-if="!successMessage && token" @submit.prevent="handleReset" class="space-y-4">
        <div>
          <label class="block text-xs font-bold text-slate-500 uppercase mb-1">Nueva Contraseña</label>
          <input 
            v-model="newPassword" 
            type="password" 
            required 
            minlength="8"
            class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:outline-none focus:border-purple-500"
          />
        </div>
        <div>
          <label class="block text-xs font-bold text-slate-500 uppercase mb-1">Confirmar Contraseña</label>
          <input 
            v-model="confirmPassword" 
            type="password" 
            required 
            class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:outline-none focus:border-purple-500"
          />
        </div>
        <button 
          type="submit" 
          class="w-full py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold rounded-xl shadow-md hover:opacity-90 transition text-sm"
        >
          Guardar Nueva Contraseña
        </button>
      </form>
    </div>
  </div>
</template>