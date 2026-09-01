<script setup>
import { ref, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import bitacoraService from '../services/bitacoraService'
import { usePermissions } from '../composables/usePermissions'

const { can } = usePermissions()

const logs = ref([])
const isLoading = ref(true)
const selectedLog = ref(null)
const showDetailModal = ref(false)

const alertModal = ref({
  show: false,
  title: '',
  message: '',
  type: 'error'
})

const filtros = ref({
  busqueda: '',
  modulo: '',
  accion: '',
  fechaInicio: '',
  fechaFin: ''
})

const showAlert = (title, message, type = 'error') => {
  alertModal.value = { show: true, title, message, type }
}

const formatFecha = (fechaStr) => {
  if (!fechaStr) return ''
  const d = new Date(fechaStr)
  return d.toLocaleString('es-GT', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

const cargarBitacora = async () => {
  if (!can('USUARIOS', 'consultar')) {
    isLoading.value = false
    return
  }

  // Validación visual de coherencia de rango de fechas
  if (filtros.value.fechaInicio && filtros.value.fechaFin) {
    if (new Date(filtros.value.fechaInicio) > new Date(filtros.value.fechaFin)) {
      showAlert('Rango de Fechas Inválido', 'La fecha de inicio no puede ser posterior a la fecha final.')
      return
    }
  }

  isLoading.value = true
  try {
    const params = {}
    if (filtros.value.busqueda) params.busqueda = filtros.value.busqueda
    if (filtros.value.modulo) params.modulo = filtros.value.modulo
    if (filtros.value.accion) params.accion = filtros.value.accion
    if (filtros.value.fechaInicio) params.fechaInicio = filtros.value.fechaInicio
    if (filtros.value.fechaFin) params.fechaFin = filtros.value.fechaFin

    const res = await bitacoraService.obtenerRegistros(params)
    logs.value = res.data
  } catch (error) {
    showAlert('Error de Servidor', 'No se pudieron recuperar los registros de auditoría.')
  } finally {
    isLoading.value = false
  }
}

const limpiarFiltros = () => {
  filtros.value = { busqueda: '', modulo: '', accion: '', fechaInicio: '', fechaFin: '' }
  cargarBitacora()
}

const abrirDetalle = (log) => {
  selectedLog.value = log
  showDetailModal.value = true
}

const getAccionBadgeClass = (accion) => {
  switch (accion?.toUpperCase()) {
    case 'INSERT': return 'bg-emerald-50 text-emerald-600 border-emerald-200'
    case 'UPDATE': return 'bg-amber-50 text-amber-600 border-amber-200'
    case 'UPDATE_PERMISOS': return 'bg-indigo-50 text-indigo-600 border-indigo-200'
    case 'STATE_CHANGE': return 'bg-purple-50 text-purple-600 border-purple-200'
    case 'LOGIN': return 'bg-blue-50 text-blue-600 border-blue-200'
    case 'DELETE': return 'bg-rose-50 text-rose-600 border-rose-200'
    default: return 'bg-slate-50 text-slate-600 border-slate-200'
  }
}

onMounted(() => {
  cargarBitacora()
})
</script>

<template>
  <MainLayout>
    <!-- Modal Central de Notificación / Alerta (Z-Index 70) -->
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

    <!-- Advertencia Sin Permiso -->
    <div v-if="!can('USUARIOS', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar la bitácora de auditoría.</p>
    </div>

    <!-- Contenido de Bitácora -->
    <div v-else class="space-y-4">
      <!-- Filtros Avanzados -->
      <div class="bg-white rounded-2xl p-5 shadow-md border border-slate-100">
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-4 border-b border-slate-100 gap-2">
          <div>
            <h2 class="text-base font-bold text-slate-800">Bitácora de Auditoría del Sistema</h2>
            <p class="text-xs text-slate-400 mt-0.5">Trazabilidad de modificaciones, inserciones y accesos de usuario.</p>
          </div>
          <button 
            @click="cargarBitacora" 
            class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-1.5"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            Buscar
          </button>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3 pt-4">
          <!-- Búsqueda General -->
          <div>
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Usuario / Registro</label>
            <input 
              v-model="filtros.busqueda" 
              @keyup.enter="cargarBitacora"
              type="text" 
              placeholder="Buscar por usuario..." 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500"
            />
          </div>

          <!-- Filtro Acción -->
          <div>
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Acción</label>
            <select 
              v-model="filtros.accion" 
              @change="cargarBitacora"
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700"
            >
              <option value="">Todas las acciones</option>
              <option value="LOGIN">LOGIN (Acceso al Sistema)</option>
              <option value="INSERT">INSERT (Inserción)</option>
              <option value="UPDATE">UPDATE (Modificación)</option>
              <option value="UPDATE_PERMISOS">UPDATE_PERMISOS (Matriz)</option>
              <option value="STATE_CHANGE">STATE_CHANGE (Estado)</option>
              <option value="DELETE">DELETE (Baja)</option>
            </select>
          </div>

          <!-- Fecha Inicio -->
          <div>
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Fecha Inicio</label>
            <input 
              v-model="filtros.fechaInicio" 
              @change="cargarBitacora"
              type="date" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700"
            />
          </div>

          <!-- Fecha Fin -->
          <div>
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Fecha Fin</label>
            <input 
              v-model="filtros.fechaFin" 
              @change="cargarBitacora"
              type="date" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700"
            />
          </div>

          <!-- Botón Limpiar -->
          <div class="flex items-end">
            <button 
              @click="limpiarFiltros"
              class="w-full py-2 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl text-xs font-semibold transition"
            >
              Limpiar Filtros
            </button>
          </div>
        </div>
      </div>

      <!-- Tabla de Resultados -->
      <div class="bg-white rounded-2xl p-6 shadow-md border border-slate-100">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 text-[11px] font-bold text-slate-400 uppercase tracking-wider">
                <th class="py-3 px-4">Fecha y Hora</th>
                <th class="py-3 px-4">Usuario</th>
                <th class="py-3 px-4">Módulo</th>
                <th class="py-3 px-4">Tabla / ID</th>
                <th class="py-3 px-4 text-center">Acción</th>
                <th class="py-3 px-4 text-center">IP</th>
                <th class="py-3 px-4 text-right">Detalles</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="log in logs" :key="log.idBitacora" class="hover:bg-slate-50/60 transition">
                <td class="py-3 px-4 text-slate-600 font-medium whitespace-nowrap">{{ formatFecha(log.fechaRegistro) }}</td>
                <td class="py-3 px-4 font-semibold text-slate-800 flex items-center gap-2">
                  <div class="w-7 h-7 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center font-bold text-[10px] uppercase">
                    {{ log.nombreUsuario?.slice(0, 2) }}
                  </div>
                  {{ log.nombreUsuario }}
                </td>
                <td class="py-3 px-4 text-slate-600">{{ log.modulo }}</td>
                <td class="py-3 px-4 text-slate-500 font-mono text-[11px]">
                  {{ log.tablaAfectada }} #{{ log.idRegistro }}
                </td>
                <td class="py-3 px-4 text-center">
                  <span :class="['px-2.5 py-1 rounded-full text-[10px] font-bold border', getAccionBadgeClass(log.accion)]">
                    {{ log.accion }}
                  </span>
                </td>
                <td class="py-3 px-4 text-center text-slate-400 font-mono text-[11px]">{{ log.direccionIp || '127.0.0.1' }}</td>
                <td class="py-3 px-4 text-right">
                  <button 
                    @click="abrirDetalle(log)"
                    class="px-3 py-1.5 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition inline-flex items-center gap-1 shadow-sm"
                  >
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                    Ver Payload
                  </button>
                </td>
              </tr>
              <tr v-if="logs.length === 0 && !isLoading">
                <td colspan="7" class="py-8 text-center text-slate-400 text-xs italic">
                  No se encontraron registros de auditoría que coincidan con los filtros.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal de Detalle / Inspección de Payload JSON -->
    <div v-if="showDetailModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-2xl border border-slate-100 max-h-[85vh] flex flex-col">
        <div class="flex items-center justify-between pb-3 border-b border-slate-100">
          <div>
            <h3 class="text-sm font-bold text-slate-800">Inspección de Registro #{{ selectedLog?.idBitacora }}</h3>
            <p class="text-[11px] text-slate-400">{{ selectedLog?.modulo }} • {{ selectedLog?.tablaAfectada }} (ID: {{ selectedLog?.idRegistro }})</p>
          </div>
          <button @click="showDetailModal = false" class="text-slate-400 hover:text-slate-600 text-base">✕</button>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4 my-4 overflow-y-auto pr-1 flex-1">
          <div>
            <h4 class="text-xs font-bold text-slate-600 mb-1.5 uppercase tracking-wider">Estado Anterior</h4>
            <div class="p-3 bg-slate-900 rounded-xl text-emerald-400 font-mono text-[11px] overflow-x-auto min-h-[140px] whitespace-pre-wrap">
              {{ selectedLog?.valoresAnteriores ? JSON.stringify(JSON.parse(selectedLog.valoresAnteriores), null, 2) : '// Sin datos previos (Nuevo registro)' }}
            </div>
          </div>

          <div>
            <h4 class="text-xs font-bold text-slate-600 mb-1.5 uppercase tracking-wider">Estado Nuevo (Actualizado)</h4>
            <div class="p-3 bg-slate-900 rounded-xl text-purple-300 font-mono text-[11px] overflow-x-auto min-h-[140px] whitespace-pre-wrap">
              {{ selectedLog?.valoresNuevos ? JSON.stringify(JSON.parse(selectedLog.valoresNuevos), null, 2) : '// Sin cambios registrados' }}
            </div>
          </div>
        </div>

        <div class="flex justify-end pt-3 border-t border-slate-100">
          <button @click="showDetailModal = false" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
            Cerrar
          </button>
        </div>
      </div>
    </div>
  </MainLayout>
</template>