<script setup>
import { ref, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import usuarioService from '../services/usuarioService'

const usuarios = ref([])
const rolesDisponibles = ref([])
const isLoading = ref(true)
const showModal = ref(false)
const isEditing = ref(false)
const currentUserId = ref(null)

const toast = ref({ show: false, message: '', type: 'success' })

const formUsuario = ref({
  nombreUsuario: '',
  correo: '',
  password: '',
  rolesIds: []
})

const showToast = (message, type = 'success') => {
  toast.value = { show: true, message, type }
  setTimeout(() => { toast.value.show = false }, 3000)
}

const cargarDatos = async () => {
  isLoading.value = true
  try {
    const [resUsuarios, resRoles] = await Promise.all([
      usuarioService.obtenerTodos(),
      usuarioService.obtenerRoles()
    ])
    usuarios.value = resUsuarios.data
    rolesDisponibles.value = resRoles.data
  } catch (error) {
    showToast('Error al sincronizar con el servidor.', 'error')
  } finally {
    isLoading.value = false
  }
}

const abrirModalCrear = () => {
  isEditing.value = false
  currentUserId.value = null
  formUsuario.value = { nombreUsuario: '', correo: '', password: '', rolesIds: [] }
  showModal.value = true
}

const abrirModalEditar = (user) => {
  isEditing.value = true
  currentUserId.value = user.idUsuario
  formUsuario.value = {
    nombreUsuario: user.nombreUsuario,
    correo: user.correo,
    password: '', // Vacío para no sobreescribir si no se desea cambiar
    rolesIds: [...user.rolesIds]
  }
  showModal.value = true
}

const guardarUsuario = async () => {
  try {
    if (isEditing.value) {
      await usuarioService.actualizar(currentUserId.value, formUsuario.value)
      showToast('Usuario actualizado exitosamente.')
    } else {
      if (!formUsuario.value.password) {
        showToast('Debes asignar una contraseña inicial.', 'error')
        return
      }
      await usuarioService.crear(formUsuario.value)
      showToast('Usuario creado exitosamente.')
    }
    showModal.value = false
    cargarDatos()
  } catch (err) {
    showToast(err.response?.data?.message || 'Error al procesar solicitud.', 'error')
  }
}

const toggleEstado = async (id) => {
  try {
    const res = await usuarioService.cambiarEstado(id)
    showToast(res.data.message)
    cargarDatos()
  } catch (err) {
    showToast('No se pudo modificar el estado.', 'error')
  }
}

onMounted(() => {
  cargarDatos()
})
</script>

<template>
  <MainLayout>
    <!-- Toast Flotante -->
    <div 
      v-if="toast.show" 
      :class="[
        'fixed top-5 right-5 z-50 px-4 py-2.5 rounded-xl shadow-lg text-xs font-semibold text-white transition-all',
        toast.type === 'success' ? 'bg-emerald-500' : 'bg-rose-500'
      ]"
    >
      {{ toast.message }}
    </div>

    <div class="bg-white rounded-2xl p-6 shadow-md border border-slate-100">
      <!-- Encabezado -->
      <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-6 gap-4 border-b border-slate-100">
        <div>
          <h2 class="text-base font-bold text-slate-800">Administración de Usuarios y Roles</h2>
          <p class="text-xs text-slate-400 mt-0.5">Control de cuentas, accesos y permisos granulares.</p>
        </div>
        <button 
          @click="abrirModalCrear"
          class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:shadow-purple-500/20 hover:opacity-95 transition flex items-center gap-2"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Nuevo Usuario
        </button>
      </div>

      <!-- Tabla -->
      <div class="overflow-x-auto mt-4">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="border-b border-slate-100 text-[11px] font-bold text-slate-400 uppercase tracking-wider">
              <th class="py-3 px-4">Usuario</th>
              <th class="py-3 px-4">Correo</th>
              <th class="py-3 px-4">Roles Asignados</th>
              <th class="py-3 px-4 text-center">Estado</th>
              <th class="py-3 px-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100 text-xs">
            <tr v-for="user in usuarios" :key="user.idUsuario" class="hover:bg-slate-50/60 transition">
              <td class="py-3.5 px-4 font-semibold text-slate-800 flex items-center gap-3">
                <div class="w-8 h-8 rounded-xl bg-slate-100 text-purple-700 flex items-center justify-center font-bold text-xs uppercase shadow-inner">
                  {{ user.nombreUsuario.slice(0, 2) }}
                </div>
                {{ user.nombreUsuario }}
              </td>
              <td class="py-3.5 px-4 text-slate-500">{{ user.correo }}</td>
              <td class="py-3.5 px-4">
                <div class="flex flex-wrap gap-1">
                  <span 
                    v-for="rol in user.roles" 
                    :key="rol" 
                    class="px-2 py-0.5 rounded-md text-[10px] font-bold bg-purple-50 text-purple-700 border border-purple-100"
                  >
                    {{ rol }}
                  </span>
                  <span v-if="user.roles.length === 0" class="text-slate-400 text-[11px] italic">Sin rol asignado</span>
                </div>
              </td>
              <td class="py-3.5 px-4 text-center">
                <span 
                  :class="[
                    'px-2.5 py-1 rounded-full text-[10px] font-bold',
                    user.estadoUsuario ? 'bg-emerald-50 text-emerald-600 border border-emerald-100' : 'bg-rose-50 text-rose-600 border border-rose-100'
                  ]"
                >
                  {{ user.estadoUsuario ? 'Activo' : 'Inactivo' }}
                </span>
              </td>
              <td class="py-3.5 px-4 text-right">
                <div class="flex items-center justify-end gap-2">
                  <!-- Botón Editar -->
                  <button 
                    @click="abrirModalEditar(user)"
                    class="px-3 py-1.5 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-sm"
                  >
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                    </svg>
                    Editar
                  </button>

                  <!-- Botón Activar / Desactivar -->
                  <button 
                    @click="toggleEstado(user.idUsuario)"
                    :class="[
                      'px-3 py-1.5 rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-sm',
                      user.estadoUsuario 
                        ? 'bg-rose-50 hover:bg-rose-600 text-rose-600 hover:text-white border border-rose-200' 
                        : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-600 hover:text-white border border-emerald-200'
                    ]"
                  >
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
                    </svg>
                    {{ user.estadoUsuario ? 'Desactivar' : 'Activar' }}
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Modal Formulario Dinámico -->
    <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-md border border-slate-100">
        <h3 class="text-base font-bold text-slate-800 mb-1">
          {{ isEditing ? 'Editar Usuario' : 'Registrar Nuevo Usuario' }}
        </h3>
        <p class="text-xs text-slate-400 mb-4">Ingresa la información general y asigna los roles correspondientes.</p>

        <form @submit.prevent="guardarUsuario" class="space-y-3.5">
          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Nombre Completo</label>
            <input v-model="formUsuario.nombreUsuario" required type="text" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Correo Electrónico</label>
            <input v-model="formUsuario.correo" required type="email" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">
              Contraseña <span v-if="isEditing" class="text-[10px] text-slate-400 font-normal">(Dejar en blanco para no cambiar)</span>
            </label>
            <input v-model="formUsuario.password" :required="!isEditing" type="password" placeholder="••••••••" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
          </div>

          <!-- Selección múltiple de Roles -->
          <div>
            <label class="block text-xs font-bold text-slate-600 mb-2 uppercase tracking-wider">Asignación de Roles</label>
            <div class="grid grid-cols-1 gap-2 max-h-36 overflow-y-auto p-2 bg-slate-50 border border-slate-200 rounded-xl">
              <label 
                v-for="rol in rolesDisponibles" 
                :key="rol.idRol" 
                class="flex items-center space-x-2 text-xs text-slate-700 cursor-pointer hover:bg-white p-1.5 rounded-lg transition"
              >
                <input 
                  type="checkbox" 
                  :value="rol.idRol" 
                  v-model="formUsuario.rolesIds" 
                  class="rounded border-slate-300 text-purple-600 focus:ring-purple-500 h-4 w-4"
                />
                <span class="font-semibold">{{ rol.nombreRol }}</span>
              </label>
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-4 border-t border-slate-100">
            <button type="button" @click="showModal = false" class="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl transition">
              Cancelar
            </button>
            <button type="submit" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
              {{ isEditing ? 'Guardar Cambios' : 'Crear Usuario' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </MainLayout>
</template>