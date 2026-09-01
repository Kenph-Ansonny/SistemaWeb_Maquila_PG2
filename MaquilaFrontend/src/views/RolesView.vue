<script setup>
import { ref, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import rolService from '../services/rolService'
import { usePermissions } from '../composables/usePermissions'

const { can } = usePermissions()

const roles = ref([])
const permisosModulo = ref([])
const permisosOriginales = ref([])
const rolSeleccionado = ref(null)
const isLoadingRoles = ref(true)
const isLoadingPermisos = ref(false)

// Estado para mostrar/ocultar roles inactivos
const mostrarInactivosRoles = ref(false)

const showModalRol = ref(false)
const isEditingRol = ref(false)

const formRol = ref({
  nombreRol: '',
  descripcion: ''
})

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

const showAlert = (title, message, type = 'success') => {
  alertModal.value = { show: true, title, message, type }
}

// Filtro reactivo de roles
const rolesFiltrados = computed(() => {
  return roles.value.filter(rol => {
    if (!mostrarInactivosRoles.value && !rol.estadoRol) {
      return false
    }
    return true
  })
})

const cargarRoles = async () => {
  if (!can('USUARIOS', 'consultar')) {
    isLoadingRoles.value = false
    return
  }
  isLoadingRoles.value = true
  try {
    const res = await rolService.obtenerRoles()
    roles.value = res.data
    
    // Seleccionar automáticamente el primer rol disponible en la lista filtrada
    if (rolesFiltrados.value.length > 0 && (!rolSeleccionado.value || !rolesFiltrados.value.some(r => r.idRol === rolSeleccionado.value.idRol))) {
      seleccionarRol(rolesFiltrados.value[0])
    }
  } catch {
    showAlert('Error', 'No se pudieron cargar los roles del sistema.', 'error')
  } finally {
    isLoadingRoles.value = false
  }
}

const seleccionarRol = async (rol) => {
  rolSeleccionado.value = rol
  isLoadingPermisos.value = true
  try {
    const res = await rolService.obtenerPermisos(rol.idRol)
    permisosModulo.value = JSON.parse(JSON.stringify(res.data))
    permisosOriginales.value = JSON.parse(JSON.stringify(res.data))
  } catch {
    showAlert('Error', 'No se pudieron cargar los permisos del rol.', 'error')
  } finally {
    isLoadingPermisos.value = false
  }
}

const descartarCambiosPermisos = () => {
  permisosModulo.value = JSON.parse(JSON.stringify(permisosOriginales.value))
}

const abrirModalCrearRol = () => {
  isEditingRol.value = false
  formRol.value = { nombreRol: '', descripcion: '' }
  showModalRol.value = true
}

const abrirModalEditarRol = (rol) => {
  isEditingRol.value = true
  formRol.value = { nombreRol: rol.nombreRol, descripcion: rol.descripcion || '' }
  showModalRol.value = true
}

const guardarRol = async () => {
  if (!formRol.value.nombreRol.trim()) {
    showAlert('Campo Requerido', 'El nombre del rol es obligatorio.', 'error')
    return
  }

  try {
    if (isEditingRol.value) {
      await rolService.actualizar(rolSeleccionado.value.idRol, formRol.value)
      showAlert('Éxito', 'Información del rol actualizada correctamente.')
    } else {
      await rolService.crear(formRol.value)
      showAlert('Éxito', 'Nuevo rol creado con su matriz inicial de permisos.')
    }
    showModalRol.value = false
    await cargarRoles()
  } catch (err) {
    showAlert('Error', err.response?.data?.message || 'Error al procesar la solicitud.', 'error')
  }
}

const togglePermisoAll = (tipo) => {
  const allActive = permisosModulo.value.every(p => p[tipo])
  permisosModulo.value.forEach(p => {
    p[tipo] = !allActive
  })
}

const guardarMatrizPermisos = async () => {
  if (!rolSeleccionado.value) return
  try {
    await rolService.guardarPermisos(rolSeleccionado.value.idRol, permisosModulo.value)
    permisosOriginales.value = JSON.parse(JSON.stringify(permisosModulo.value))
    showAlert('Permisos Actualizados', `Se guardaron los cambios para el rol "${rolSeleccionado.value.nombreRol}".`)
  } catch {
    showAlert('Error', 'No se pudieron actualizar los permisos.', 'error')
  }
}

const solicitarToggleEstado = (rol) => {
  const accion = rol.estadoRol ? 'desactivar' : 'activar'
  confirmModal.value = {
    show: true,
    title: '¿Confirmar cambio de estado?',
    message: `¿Estás seguro de que deseas ${accion} el rol "${rol.nombreRol}"?`,
    action: async () => {
      confirmModal.value.show = false
      try {
        const res = await rolService.cambiarEstado(rol.idRol)
        showAlert('Estado Actualizado', res.data.message)
        cargarRoles()
      } catch (err) {
        showAlert('Error', err.response?.data?.message || 'No se pudo modificar el estado.', 'error')
      }
    },
    type: rol.estadoRol ? 'danger' : 'success'
  }
}

onMounted(() => {
  cargarRoles()
})
</script>

<template>
  <MainLayout>
    <!-- Modal Central de Notificación (Z-Index 70) -->
    <div v-if="alertModal.show" class="fixed inset-0 z-[70] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
        <div :class="['w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-3', alertModal.type === 'success' ? 'bg-emerald-50 text-emerald-500' : 'bg-rose-50 text-rose-500']">
          <svg v-if="alertModal.type === 'success'" class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
          <svg v-else class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
        </div>
        <h3 class="text-base font-bold text-slate-800">{{ alertModal.title }}</h3>
        <p class="text-xs text-slate-500 mt-1 mb-5">{{ alertModal.message }}</p>
        <button @click="alertModal.show = false" class="w-full py-2.5 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">Aceptar</button>
      </div>
    </div>

    <!-- Modal Central de Confirmación (Z-Index 60) -->
    <div v-if="confirmModal.show" class="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
        <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4">
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
        </div>
        <h3 class="text-base font-bold text-slate-800">{{ confirmModal.title }}</h3>
        <p class="text-xs text-slate-500 mt-2 mb-6">{{ confirmModal.message }}</p>
        <div class="flex justify-center gap-3">
          <button @click="confirmModal.show = false" class="px-4 py-2 text-xs font-semibold text-slate-500 bg-slate-100 hover:bg-slate-200 rounded-xl transition">Cancelar</button>
          <button @click="confirmModal.action" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">Confirmar</button>
        </div>
      </div>
    </div>

    <!-- Layout Split-View -->
    <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
      <!-- Columna Izquierda: Roles -->
      <div class="lg:col-span-4 bg-white rounded-2xl p-5 shadow-md border border-slate-100 space-y-4">
        <div class="flex items-center justify-between pb-3 border-b border-slate-100">
          <div>
            <h2 class="text-sm font-bold text-slate-800">Roles del Sistema</h2>
            <p class="text-[11px] text-slate-400">Selecciona para configurar privilegios</p>
          </div>
          <button 
            v-if="can('USUARIOS', 'insertar')" 
            @click="abrirModalCrearRol" 
            class="px-3 py-1.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-1.5"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
            Nuevo Rol
          </button>
        </div>

        <!-- Checkbox Mostrar Inactivos -->
        <div class="pt-1">
          <label class="flex items-center gap-2 text-xs font-semibold text-slate-600 cursor-pointer select-none">
            <input 
              type="checkbox" 
              v-model="mostrarInactivosRoles" 
              class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500"
            />
            <span>Mostrar roles inactivos</span>
          </label>
        </div>

        <!-- Lista de Roles Filtrada -->
        <div class="space-y-2.5 max-h-[550px] overflow-y-auto pr-1">
          <div 
            v-for="rol in rolesFiltrados" 
            :key="rol.idRol"
            @click="seleccionarRol(rol)"
            :class="[
              'p-4 rounded-xl border transition-all cursor-pointer space-y-3',
              rolSeleccionado?.idRol === rol.idRol 
                ? 'bg-purple-50/70 border-purple-400 shadow-sm' 
                : 'bg-white border-slate-200 hover:border-slate-300 hover:bg-slate-50/60'
            ]"
          >
            <div class="flex items-start justify-between gap-2">
              <div>
                <span class="font-bold text-xs text-slate-800">{{ rol.nombreRol }}</span>
                <p class="text-[11px] text-slate-400 mt-0.5">{{ rol.descripcion || 'Sin descripción' }}</p>
                <span class="text-[10px] text-purple-700 font-semibold mt-1 inline-block">{{ rol.cantidadUsuarios }} usuario(s)</span>
              </div>
              <span :class="['px-2 py-0.5 rounded-full text-[10px] font-bold', rol.estadoRol ? 'bg-emerald-50 text-emerald-600 border border-emerald-200' : 'bg-rose-50 text-rose-600 border border-rose-200']">
                {{ rol.estadoRol ? 'Activo' : 'Inactivo' }}
              </span>
            </div>

            <div class="flex items-center gap-2 pt-2 border-t border-slate-100">
              <button 
                v-if="can('USUARIOS', 'modificar')" 
                @click.stop="abrirModalEditarRol(rol)" 
                class="flex-1 py-1.5 px-2 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition flex items-center justify-center gap-1 shadow-sm"
              >
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" /></svg>
                Editar
              </button>

              <button 
                v-if="can('USUARIOS', 'eliminar') && rol.idRol !== 1" 
                @click.stop="solicitarToggleEstado(rol)" 
                :class="[
                  'flex-1 py-1.5 px-2 rounded-lg text-xs font-semibold transition flex items-center justify-center gap-1 shadow-sm border',
                  rol.estadoRol ? 'bg-rose-50 hover:bg-rose-600 text-rose-600 hover:text-white border-rose-200' : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-600 hover:text-white border-emerald-200'
                ]"
              >
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" /></svg>
                {{ rol.estadoRol ? 'Desactivar' : 'Activar' }}
              </button>
            </div>
          </div>

          <div v-if="rolesFiltrados.length === 0 && !isLoadingRoles" class="py-6 text-center text-slate-400 text-xs italic">
            No hay roles para mostrar.
          </div>
        </div>
      </div>

      <!-- Columna Derecha: Matriz Granular de Permisos -->
      <div class="lg:col-span-8 bg-white rounded-2xl p-6 shadow-md border border-slate-100">
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-5 border-b border-slate-100 gap-3">
          <div>
            <h2 class="text-base font-bold text-slate-800">Matriz de Permisos: {{ rolSeleccionado?.nombreRol }}</h2>
            <p class="text-xs text-slate-400 mt-0.5">Controla las operaciones permitidas para este perfil.</p>
          </div>

          <div class="flex items-center gap-2">
            <button 
              @click="descartarCambiosPermisos" 
              class="px-3 py-2 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl text-xs font-semibold transition"
            >
              Descartar
            </button>
            <button 
              v-if="can('USUARIOS', 'modificar')" 
              @click="guardarMatrizPermisos" 
              class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-1.5"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4" /></svg>
              Guardar Permisos
            </button>
          </div>
        </div>

        <div class="overflow-x-auto mt-4">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 text-[11px] font-bold text-slate-400 uppercase tracking-wider">
                <th class="py-3 px-3">Módulo / Pantalla</th>
                <th class="py-3 px-3 text-center cursor-pointer hover:text-purple-600 select-none" @click="togglePermisoAll('puedeConsultar')">Consultar ⇅</th>
                <th class="py-3 px-3 text-center cursor-pointer hover:text-purple-600 select-none" @click="togglePermisoAll('puedeInsertar')">Insertar ⇅</th>
                <th class="py-3 px-3 text-center cursor-pointer hover:text-purple-600 select-none" @click="togglePermisoAll('puedeModificar')">Modificar ⇅</th>
                <th class="py-3 px-3 text-center cursor-pointer hover:text-purple-600 select-none" @click="togglePermisoAll('puedeEliminar')">Desactivar ⇅</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="item in permisosModulo" :key="item.idModulo" class="hover:bg-slate-50/60 transition">
                <td class="py-3 px-3 font-semibold text-slate-700">
                  <span class="block">{{ item.nombreModulo }}</span>
                  <span class="text-[10px] text-slate-400 font-mono">{{ item.codigoModulo }}</span>
                </td>
                
                <td class="py-3 px-3 text-center">
                  <input type="checkbox" v-model="item.puedeConsultar" class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500 cursor-pointer" />
                </td>

                <td class="py-3 px-3 text-center">
                  <input type="checkbox" v-model="item.puedeInsertar" class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500 cursor-pointer" />
                </td>

                <td class="py-3 px-3 text-center">
                  <input type="checkbox" v-model="item.puedeModificar" class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500 cursor-pointer" />
                </td>

                <td class="py-3 px-3 text-center">
                  <input type="checkbox" v-model="item.puedeEliminar" class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500 cursor-pointer" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Formulario de Rol -->
    <div v-if="showModalRol" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100">
        <h3 class="text-base font-bold text-slate-800 mb-1">{{ isEditingRol ? 'Editar Rol' : 'Nuevo Rol' }}</h3>
        <p class="text-xs text-slate-400 mb-4">Información general del perfil de acceso[cite: 1].</p>

        <form @submit.prevent="guardarRol" class="space-y-3.5">
          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Nombre del Rol</label>
            <input v-model="formRol.nombreRol" required type="text" placeholder="Ej: Encargado de Bodega" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Descripción</label>
            <textarea v-model="formRol.descripcion" rows="3" placeholder="Propósito del rol..." class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500"></textarea>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-100">
            <button type="button" @click="showModalRol = false" class="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl transition">Cancelar</button>
            <button type="submit" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">{{ isEditingRol ? 'Guardar Cambios' : 'Crear Rol' }}</button>
          </div>
        </form>
      </div>
    </div>
  </MainLayout>
</template>