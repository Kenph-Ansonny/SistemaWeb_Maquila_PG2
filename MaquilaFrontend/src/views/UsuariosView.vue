<script setup>
import { ref, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import usuarioService from '../services/usuarioService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, PasswordRules, EmailRules } from '../utils/validators'

const { can } = usePermissions()

const usuarios = ref([])
const rolesDisponibles = ref([])
const isLoading = ref(true)
const showModal = ref(false)
const isEditing = ref(false)
const currentUserId = ref(null)

// Filtros de búsqueda reactiva
const filtroBusqueda = ref('')
const filtroRol = ref('')
const mostrarInactivos = ref(false)

// Modales centrales
const confirmModal = ref({
  show: false,
  title: '',
  message: '',
  action: null,
  type: 'warning'
})

const alertModal = ref({
  show: false,
  title: '',
  message: '',
  type: 'success'
})

const confirmPassword = ref('')
const showPassword = ref(false)

const formUsuario = ref({
  nombreUsuario: '',
  correo: '',
  password: '',
  rolesIds: []
})

const showAlert = (title, message, type = 'success') => {
  alertModal.value = { show: true, title, message, type }
}

const formatFecha = (fechaStr) => {
  if (!fechaStr) return 'Nunca'
  const d = new Date(fechaStr)
  return d.toLocaleString('es-GT', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// Filtro reactivo en memoria
const usuariosFiltrados = computed(() => {
  return usuarios.value.filter(u => {
    // 1. Filtro Mostrar Inactivos
    if (!mostrarInactivos.value && !u.estadoUsuario) {
      return false
    }

    // 2. Filtro por Rol
    if (filtroRol.value && !u.roles.includes(filtroRol.value)) {
      return false
    }

    // 3. Filtro por Búsqueda (Usuario / Correo)
    if (filtroBusqueda.value.trim()) {
      const term = filtroBusqueda.value.toLowerCase()
      const matchName = u.nombreUsuario.toLowerCase().includes(term)
      const matchEmail = u.correo.toLowerCase().includes(term)
      const matchRole = u.roles.some(r => r.toLowerCase().includes(term))
      return matchName || matchEmail || matchRole
    }

    return true
  })
})

const emailEvaluation = computed(() => {
  if (!formUsuario.value.correo) return { isValid: false }
  return EmailRules.validate(formUsuario.value.correo)
})

const passwordEvaluation = computed(() => {
  if (!formUsuario.value.password) return { isValid: false }
  return PasswordRules.validate(formUsuario.value.password)
})

const passwordMismatch = computed(() => {
  if (!formUsuario.value.password && !confirmPassword.value) return false
  return formUsuario.value.password !== confirmPassword.value
})

const passwordMatch = computed(() => {
  return formUsuario.value.password && 
         confirmPassword.value && 
         formUsuario.value.password === confirmPassword.value && 
         passwordEvaluation.value.isValid
})

const cargarDatos = async () => {
  if (!can('USUARIOS', 'consultar')) {
    isLoading.value = false
    return
  }
  isLoading.value = true
  try {
    const [resUsuarios, resRoles] = await Promise.all([
      usuarioService.obtenerTodos(),
      usuarioService.obtenerRoles()
    ])
    usuarios.value = resUsuarios.data
    rolesDisponibles.value = resRoles.data
  } catch (error) {
    showAlert('Error de Comunicación', 'No se pudieron cargar los datos del servidor.', 'error')
  } finally {
    isLoading.value = false
  }
}

const abrirModalCrear = () => {
  isEditing.value = false
  currentUserId.value = null
  confirmPassword.value = ''
  showPassword.value = false
  formUsuario.value = { nombreUsuario: '', correo: '', password: '', rolesIds: [] }
  showModal.value = true
}

const abrirModalEditar = (user) => {
  isEditing.value = true
  currentUserId.value = user.idUsuario
  confirmPassword.value = ''
  showPassword.value = false
  formUsuario.value = {
    nombreUsuario: user.nombreUsuario,
    correo: user.correo,
    password: '',
    rolesIds: [...user.rolesIds]
  }
  showModal.value = true
}

const solicitarConfirmacionGuardar = () => {
  if (!emailEvaluation.value.isValid) {
    showAlert('Correo Inválido', 'El formato del correo electrónico no es válido.', 'error')
    return
  }

  if (!isEditing.value) {
    if (!passwordEvaluation.value.isValid) {
      showAlert('Contraseña Débil', 'La contraseña no cumple con los requisitos de seguridad.', 'error')
      return
    }
    if (formUsuario.value.password !== confirmPassword.value) {
      showAlert('Error de Confirmación', 'Las contraseñas no coinciden.', 'error')
      return
    }
  }

  if (isEditing.value && formUsuario.value.password) {
    if (!passwordEvaluation.value.isValid) {
      showAlert('Contraseña Débil', 'La nueva contraseña no cumple con los requisitos de seguridad.', 'error')
      return
    }
    if (formUsuario.value.password !== confirmPassword.value) {
      showAlert('Error de Confirmación', 'La confirmación de la nueva contraseña no coincide.', 'error')
      return
    }
  }

  if (isEditing.value) {
    confirmModal.value = {
      show: true,
      title: '¿Confirmar Modificación?',
      message: `Se actualizarán los datos y roles del usuario "${formUsuario.value.nombreUsuario}".`,
      action: ejecutarGuardado,
      type: 'warning'
    }
  } else {
    ejecutarGuardado()
  }
}

const ejecutarGuardado = async () => {
  confirmModal.value.show = false
  try {
    if (isEditing.value) {
      await usuarioService.actualizar(currentUserId.value, formUsuario.value)
      showModal.value = false
      showAlert('Modificación Exitosa', 'El usuario y sus permisos fueron actualizados correctamente.')
    } else {
      await usuarioService.crear(formUsuario.value)
      showModal.value = false
      showAlert('Registro Exitoso', 'El nuevo usuario fue registrado e indexado en el sistema.')
    }
    cargarDatos()
  } catch (err) {
    showAlert('Error al Procesar', err.response?.data?.message || 'Error en el servidor.', 'error')
  }
}

const solicitarConfirmacionEstado = (user) => {
  const accion = user.estadoUsuario ? 'desactivar y bloquear' : 'activar'
  confirmModal.value = {
    show: true,
    title: `¿Confirmar cambio de estado?`,
    message: `¿Estás seguro de que deseas ${accion} la cuenta de "${user.nombreUsuario}"?`,
    action: () => ejecutarToggleEstado(user.idUsuario),
    type: user.estadoUsuario ? 'danger' : 'success'
  }
}

const ejecutarToggleEstado = async (id) => {
  confirmModal.value.show = false
  try {
    const res = await usuarioService.cambiarEstado(id)
    showAlert('Estado Actualizado', res.data.message)
    cargarDatos()
  } catch (err) {
    showAlert('Error', 'No se pudo modificar el estado de la cuenta.', 'error')
  }
}

onMounted(() => {
  cargarDatos()
})
</script>

<template>
  <MainLayout>
    <!-- Modal Central de Notificación (Prioridad Z-Index 70) -->
    <div v-if="alertModal.show" class="fixed inset-0 z-[70] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
        <div 
          :class="[
            'w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-3',
            alertModal.type === 'success' ? 'bg-emerald-50 text-emerald-500' : 'bg-rose-50 text-rose-500'
          ]"
        >
          <svg v-if="alertModal.type === 'success'" class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
          </svg>
          <svg v-else class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </div>
        <h3 class="text-base font-bold text-slate-800">{{ alertModal.title }}</h3>
        <p class="text-xs text-slate-500 mt-1 mb-5">{{ alertModal.message }}</p>
        <button 
          @click="alertModal.show = false" 
          class="w-full py-2.5 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition"
        >
          Aceptar
        </button>
      </div>
    </div>

    <!-- Modal Central de Confirmación (Prioridad Z-Index 60) -->
    <div v-if="confirmModal.show" class="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
        <div 
          :class="[
            'w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-4',
            confirmModal.type === 'danger' ? 'bg-rose-50 text-rose-500' : 'bg-amber-50 text-amber-500'
          ]"
        >
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
        </div>
        <h3 class="text-base font-bold text-slate-800">{{ confirmModal.title }}</h3>
        <p class="text-xs text-slate-500 mt-2 mb-6">{{ confirmModal.message }}</p>
        <div class="flex justify-center gap-3">
          <button @click="confirmModal.show = false" class="px-4 py-2 text-xs font-semibold text-slate-500 bg-slate-100 hover:bg-slate-200 rounded-xl transition">
            Cancelar
          </button>
          <button @click="confirmModal.action" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
            Confirmar
          </button>
        </div>
      </div>
    </div>

    <!-- Contenido Principal -->
    <div class="space-y-4">
      <!-- Barra de Filtros y Búsqueda -->
      <div class="bg-white rounded-2xl p-5 shadow-md border border-slate-100">
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-4 border-b border-slate-100 gap-2">
          <div>
            <h2 class="text-base font-bold text-slate-800">Administración de Cuentas y Accesos</h2>
            <p class="text-xs text-slate-400 mt-0.5">Control granular de usuarios, roles y estados de bloqueo.</p>
          </div>
          <button 
            v-if="can('USUARIOS', 'insertar')"
            @click="abrirModalCrear"
            class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-2"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Nuevo Usuario
          </button>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 gap-3 pt-4 items-center">
          <!-- Búsqueda General -->
          <div class="sm:col-span-2">
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Buscar Usuario o Correo</label>
            <div class="relative">
              <input 
                v-model="filtroBusqueda" 
                type="text" 
                placeholder="Escribe nombre, correo o rol..." 
                class="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500"
              />
              <svg class="w-4 h-4 text-slate-400 absolute left-3 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
            </div>
          </div>

          <!-- Filtro por Rol -->
          <div>
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Filtrar por Rol</label>
            <select 
              v-model="filtroRol" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700"
            >
              <option value="">Todos los roles</option>
              <option v-for="rol in rolesDisponibles" :key="rol.idRol" :value="rol.nombreRol">
                {{ rol.nombreRol }}
              </option>
            </select>
          </div>

          <!-- Checkbox Mostrar Inactivos -->
          <div class="flex items-center pt-5 sm:pt-4">
            <label class="flex items-center gap-2 text-xs font-semibold text-slate-600 cursor-pointer select-none">
              <input 
                type="checkbox" 
                v-model="mostrarInactivos" 
                class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500"
              />
              <span>Mostrar inactivos / bloqueados</span>
            </label>
          </div>
        </div>
      </div>

      <!-- Tabla de Datos -->
      <div class="bg-white rounded-2xl p-6 shadow-md border border-slate-100">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 text-[11px] font-bold text-slate-400 uppercase tracking-wider">
                <th class="py-3 px-4">Usuario</th>
                <th class="py-3 px-4">Correo</th>
                <th class="py-3 px-4">Roles</th>
                <th class="py-3 px-4">Último Acceso</th>
                <th class="py-3 px-4 text-center">Estado</th>
                <th class="py-3 px-4 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="user in usuariosFiltrados" :key="user.idUsuario" class="hover:bg-slate-50/60 transition">
                <td class="py-3.5 px-4 font-semibold text-slate-800 flex items-center gap-3">
                  <div class="w-8 h-8 rounded-xl bg-slate-100 text-purple-700 flex items-center justify-center font-bold text-xs uppercase">
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
                    <span v-if="user.roles.length === 0" class="text-slate-400 text-[11px] italic">Sin rol</span>
                  </div>
                </td>
                <td class="py-3.5 px-4 text-slate-500 text-[11px]">
                  {{ formatFecha(user.fechaUltimoAcceso) }}
                </td>
                <td class="py-3.5 px-4 text-center">
                  <div>
                    <span 
                      :class="[
                        'px-2.5 py-1 rounded-full text-[10px] font-bold',
                        user.estadoUsuario ? 'bg-emerald-50 text-emerald-600 border border-emerald-100' : 'bg-rose-50 text-rose-600 border border-rose-100'
                      ]"
                    >
                      {{ user.estadoUsuario ? 'Activo' : 'Inactivo' }}
                    </span>
                    <!-- Indicador de Fecha de Bloqueo si está inactivo -->
                    <p v-if="!user.estadoUsuario && user.fechaBloqueo" class="text-[10px] text-rose-500 font-medium mt-0.5">
                      Bloqueado: {{ formatFecha(user.fechaBloqueo) }}
                    </p>
                  </div>
                </td>
                <td class="py-3.5 px-4 text-right">
                  <div class="flex items-center justify-end gap-2">
                    <button 
                      v-if="can('USUARIOS', 'modificar')"
                      @click="abrirModalEditar(user)"
                      class="px-3 py-1.5 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition inline-flex items-center gap-1.5"
                    >
                      Editar
                    </button>
                    <button 
                      v-if="can('USUARIOS', 'eliminar')"
                      @click="solicitarConfirmacionEstado(user)"
                      :class="[
                        'px-3 py-1.5 rounded-lg text-xs font-semibold transition inline-flex items-center gap-1.5',
                        user.estadoUsuario 
                          ? 'bg-rose-50 hover:bg-rose-600 text-rose-600 hover:text-white border border-rose-200' 
                          : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-600 hover:text-white border border-emerald-200'
                      ]"
                    >
                      {{ user.estadoUsuario ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>
              <tr v-if="usuariosFiltrados.length === 0 && !isLoading">
                <td colspan="6" class="py-8 text-center text-slate-400 text-xs italic">
                  No se encontraron usuarios que coincidan con los criterios de búsqueda.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Formulario (Prioridad Z-Index 50) -->
    <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-md border border-slate-100 max-h-[90vh] overflow-y-auto">
        <h3 class="text-base font-bold text-slate-800 mb-1">
          {{ isEditing ? 'Editar Usuario' : 'Registrar Nuevo Usuario' }}
        </h3>
        <p class="text-xs text-slate-400 mb-4">Ingresa la información general y asigna los roles[cite: 1].</p>

        <form @submit.prevent="solicitarConfirmacionGuardar" class="space-y-3.5">
          <!-- Nombre de Usuario -->
          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Nombre de Usuario</label>
            <input 
              v-model="formUsuario.nombreUsuario" 
              @keypress="allowOnly.usuarioInput($event)"
              required 
              type="text" 
              placeholder="Ej: Usuprueba"
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" 
            />
          </div>

          <!-- Correo Electrónico -->
          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Correo Electrónico</label>
            <input 
              v-model="formUsuario.correo" 
              required 
              type="email" 
              placeholder="Usuprueba@correo.com"
              :class="[
                'w-full px-3 py-2 bg-slate-50 border rounded-xl text-xs outline-none transition',
                formUsuario.correo && !emailEvaluation.isValid ? 'border-rose-400 focus:border-rose-500' : 'border-slate-200 focus:border-purple-500'
              ]"
            />
            <div v-if="formUsuario.correo" class="mt-2 p-2 bg-slate-50 rounded-xl border border-slate-100 text-[11px] space-y-1">
              <div :class="emailEvaluation.hasAt ? 'text-emerald-600 font-medium' : 'text-slate-400'">• Incluye símbolo '@'</div>
              <div :class="emailEvaluation.hasDomain ? 'text-emerald-600 font-medium' : 'text-slate-400'">• Dominio válido (ej: .com, .net)</div>
              <div :class="emailEvaluation.noSpaces ? 'text-emerald-600 font-medium' : 'text-slate-400'">• Sin espacios intermedios</div>
            </div>
          </div>

          <!-- Contraseña -->
          <div>
            <div class="flex items-center justify-between mb-1">
              <label class="block text-xs font-bold text-slate-600 uppercase tracking-wider">
                Contraseña <span v-if="isEditing" class="text-[10px] text-slate-400 font-normal">(Opcional)</span>
              </label>
              <label class="flex items-center gap-1.5 text-[11px] text-slate-500 cursor-pointer">
                <input type="checkbox" v-model="showPassword" class="rounded border-slate-300 text-purple-600 h-3.5 w-3.5" />
                <span>Mostrar</span>
              </label>
            </div>
            <input 
              v-model="formUsuario.password" 
              :required="!isEditing" 
              :type="showPassword ? 'text' : 'password'" 
              placeholder="••••••••" 
              :class="[
                'w-full px-3 py-2 bg-slate-50 border rounded-xl text-xs outline-none transition',
                formUsuario.password && !passwordEvaluation.isValid ? 'border-rose-400 focus:border-rose-500' : 'border-slate-200 focus:border-purple-500'
              ]" 
            />
            <div v-if="formUsuario.password" class="mt-2 p-2 bg-slate-50 rounded-xl border border-slate-100 text-[11px] space-y-1">
              <div :class="passwordEvaluation.minLength ? 'text-emerald-600 font-medium' : 'text-slate-400'">• Mínimo 8 caracteres</div>
              <div :class="passwordEvaluation.hasUpper && passwordEvaluation.hasLower ? 'text-emerald-600 font-medium' : 'text-slate-400'">• Mayúsculas y minúsculas</div>
              <div :class="passwordEvaluation.hasNumber ? 'text-emerald-600 font-medium' : 'text-slate-400'">• Al menos un número</div>
              <div :class="passwordEvaluation.hasSpecial ? 'text-emerald-600 font-medium' : 'text-slate-400'">• Carácter especial (@, $, !, %, *, #, etc.)</div>
            </div>
          </div>

          <!-- Confirmar Contraseña -->
          <div v-if="!isEditing || formUsuario.password">
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Confirmar Contraseña</label>
            <input 
              v-model="confirmPassword" 
              :required="!isEditing || formUsuario.password.length > 0"
              :type="showPassword ? 'text' : 'password'" 
              placeholder="••••••••" 
              :class="[
                'w-full px-3 py-2 bg-slate-50 border rounded-xl text-xs outline-none transition',
                passwordMismatch ? 'border-rose-500 bg-rose-50/20 text-rose-700' : '',
                passwordMatch ? 'border-emerald-500 bg-emerald-50/20 text-emerald-700' : 'border-slate-200 focus:border-purple-500'
              ]" 
            />
          </div>

          <!-- Asignación de Roles -->
          <div>
            <label class="block text-xs font-bold text-slate-600 mb-2 uppercase tracking-wider">Asignación de Roles</label>
            <div class="grid grid-cols-1 gap-2 max-h-32 overflow-y-auto p-2 bg-slate-50 border border-slate-200 rounded-xl">
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
            <button 
              type="submit" 
              class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition"
            >
              {{ isEditing ? 'Guardar Cambios' : 'Crear Usuario' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </MainLayout>
</template>