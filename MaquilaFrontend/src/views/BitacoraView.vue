<script setup>
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import bitacoraService from '../services/bitacoraService'
import { usePermissions } from '../composables/usePermissions'

const { can } = usePermissions()

// ---------- Estados Principales ----------
const logs = ref([])
const isLoading = ref(true)
const selectedLog = ref(null)
const showDetailModal = ref(false)
const tabModal = ref('visual') // 'visual' | 'raw'
const copiadoExitoso = ref(false)

// Filtros de búsqueda
const filtros = reactive({
  busqueda: '',
  modulo: '',
  accion: '',
  fechaInicio: '',
  fechaFin: ''
})

// ---------- Toasts Notificaciones Flotantes ----------
const TOAST_VARIANTS = {
  success: {
    wrap: 'bg-white border-emerald-200 text-slate-700',
    iconWrap: 'bg-emerald-100 text-emerald-600',
    bar: 'bg-emerald-500',
    title: 'Operación Exitosa',
    icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  error: {
    wrap: 'bg-white border-rose-200 text-slate-700',
    iconWrap: 'bg-rose-100 text-rose-600',
    bar: 'bg-rose-500',
    title: 'Atención Requerida',
    icon: 'M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  warning: {
    wrap: 'bg-white border-amber-200 text-slate-700',
    iconWrap: 'bg-amber-100 text-amber-600',
    bar: 'bg-amber-500',
    title: 'Aviso de Auditoría',
    icon: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z'
  }
}

const toasts = ref([])
let toastSeq = 0

const quitarToast = (id) => {
  toasts.value = toasts.value.filter(t => t.id !== id)
}

const mostrarToast = (type, message, duracion = 4500) => {
  const id = ++toastSeq
  toasts.value.push({ id, type, message, duracion })
  setTimeout(() => quitarToast(id), duracion)
}

// ---------- Formateadores ----------
const formatFecha = (fechaStr) => {
  if (!fechaStr) return '—'
  const strUtc = fechaStr.endsWith('Z') ? fechaStr : `${fechaStr}Z`
  const d = new Date(strUtc)
  return d.toLocaleString('es-GT', {
    timeZone: 'America/Guatemala',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: true
  })
}

// Estilos e iconografía semántica por acción
const ACCION_CONFIG = {
  LOGIN: {
    label: 'Inicio de Sesión',
    badge: 'bg-sky-50 text-sky-700 border-sky-300',
    dot: 'bg-sky-500'
  },
  INSERT: {
    label: 'Inserción',
    badge: 'bg-emerald-50 text-emerald-700 border-emerald-300',
    dot: 'bg-emerald-500'
  },
  UPDATE: {
    label: 'Modificación',
    badge: 'bg-amber-50 text-amber-700 border-amber-300',
    dot: 'bg-amber-500'
  },
  STATE_CHANGE: {
    label: 'Cambio de Estado',
    badge: 'bg-purple-50 text-purple-700 border-purple-300',
    dot: 'bg-purple-500'
  },
  UPDATE_PERMISOS: {
    label: 'Matriz Privilegios',
    badge: 'bg-indigo-50 text-indigo-700 border-indigo-300',
    dot: 'bg-indigo-500'
  },
  RESET_SOLICITUD: {
    label: 'Solicitud Reset',
    badge: 'bg-orange-50 text-orange-700 border-orange-300',
    dot: 'bg-orange-500'
  },
  RESET_EXITOSO: {
    label: 'Reset Exitoso',
    badge: 'bg-teal-50 text-teal-700 border-teal-300',
    dot: 'bg-teal-500'
  },
  DELETE: {
    label: 'Eliminación',
    badge: 'bg-rose-50 text-rose-700 border-rose-300',
    dot: 'bg-rose-500'
  }
}

const getAccionBadge = (accion) => {
  return ACCION_CONFIG[accion?.toUpperCase()] || {
    label: accion || 'Evento',
    badge: 'bg-slate-50 text-slate-700 border-slate-300',
    dot: 'bg-slate-400'
  }
}

// ---------- KPIs de Auditoría ----------
const kpis = computed(() => {
  const lista = logs.value
  return {
    total: lista.length,
    logins: lista.filter(l => l.accion === 'LOGIN').length,
    cambios: lista.filter(l => ['UPDATE', 'STATE_CHANGE', 'UPDATE_PERMISOS'].includes(l.accion)).length,
    inserciones: lista.filter(l => l.accion === 'INSERT').length
  }
})

// ---------- Diff y Procesamiento de Payloads ----------
const parsedDiff = computed(() => {
  if (!selectedLog.value) return []
  let ant = {}
  let nue = {}

  try {
    ant = selectedLog.value.valoresAnteriores ? JSON.parse(selectedLog.value.valoresAnteriores) : {}
  } catch {
    ant = { Informacion: selectedLog.value.valoresAnteriores }
  }

  try {
    nue = selectedLog.value.valoresNuevos ? JSON.parse(selectedLog.value.valoresNuevos) : {}
  } catch {
    nue = { Informacion: selectedLog.value.valoresNuevos }
  }

  const allKeys = Array.from(new Set([...Object.keys(ant), ...Object.keys(nue)]))

  return allKeys.map(k => {
    const vAnt = ant[k] !== undefined ? (typeof ant[k] === 'object' ? JSON.stringify(ant[k]) : String(ant[k])) : null
    const vNue = nue[k] !== undefined ? (typeof nue[k] === 'object' ? JSON.stringify(nue[k]) : String(nue[k])) : null

    return {
      campo: k,
      anterior: vAnt,
      nuevo: vNue,
      haCambiado: vAnt !== vNue
    }
  })
})

const copiarJsonAlPortapapeles = async () => {
  if (!selectedLog.value) return
  const dataToCopy = {
    eventoId: selectedLog.value.idBitacora,
    usuario: selectedLog.value.nombreUsuario,
    modulo: selectedLog.value.modulo,
    tabla: selectedLog.value.tablaAfectada,
    registroId: selectedLog.value.idRegistro,
    accion: selectedLog.value.accion,
    ip: selectedLog.value.direccionIp,
    fecha: selectedLog.value.fechaRegistro,
    valoresAnteriores: selectedLog.value.valoresAnteriores ? JSON.parse(selectedLog.value.valoresAnteriores) : null,
    valoresNuevos: selectedLog.value.valoresNuevos ? JSON.parse(selectedLog.value.valoresNuevos) : null
  }

  try {
    await navigator.clipboard.writeText(JSON.stringify(dataToCopy, null, 2))
    copiadoExitoso.value = true
    mostrarToast('success', 'Estructura JSON copiada al portapapeles.')
    setTimeout(() => { copiadoExitoso.value = false }, 2500)
  } catch {
    mostrarToast('error', 'No se pudo copiar el JSON automáticamente.')
  }
}

// ---------- Carga de Datos ----------
const cargarBitacora = async () => {
  if (!can('USUARIOS', 'consultar')) {
    isLoading.value = false
    return
  }

  if (filtros.fechaInicio && filtros.fechaFin) {
    if (new Date(filtros.fechaInicio) > new Date(filtros.fechaFin)) {
      mostrarToast('warning', 'La fecha de inicio no puede ser posterior a la fecha final.')
      return
    }
  }

  isLoading.value = true
  try {
    const data = await bitacoraService.obtenerRegistros(filtros)
    logs.value = Array.isArray(data) ? data : (data?.data || [])
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'Error al recuperar los eventos de auditoría.')
  } finally {
    isLoading.value = false
  }
}

const limpiarFiltros = () => {
  filtros.busqueda = ''
  filtros.modulo = ''
  filtros.accion = ''
  filtros.fechaInicio = ''
  filtros.fechaFin = ''
  cargarBitacora()
}

const abrirDetalle = (log) => {
  selectedLog.value = log
  tabModal.value = 'visual'
  copiadoExitoso.value = false
  showDetailModal.value = true
}

// Control Teclado (Escape)
const onKeydown = (e) => {
  if (e.key === 'Escape' && showDetailModal.value) {
    showDetailModal.value = false
  }
}

onMounted(() => {
  window.addEventListener('keydown', onKeydown)
  cargarBitacora()
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <MainLayout>
    <!-- Toasts Notificaciones Flotantes -->
    <div class="fixed top-5 right-5 z-[80] flex flex-col gap-2 w-80 max-w-[calc(100vw-2.5rem)]" aria-live="polite">
      <TransitionGroup name="toast">
        <div
          v-for="t in toasts"
          :key="t.id"
          role="alert"
          :class="['relative overflow-hidden flex items-start gap-3 pl-3 pr-3 pt-3 pb-4 rounded-xl shadow-xl border', TOAST_VARIANTS[t.type].wrap]"
        >
          <div :class="['w-8 h-8 rounded-lg flex items-center justify-center shrink-0', TOAST_VARIANTS[t.type].iconWrap]">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="TOAST_VARIANTS[t.type].icon" />
            </svg>
          </div>
          <div class="flex-1 min-w-0 pt-0.5">
            <p class="text-xs font-black text-slate-800">{{ TOAST_VARIANTS[t.type].title }}</p>
            <p class="text-[11px] font-medium text-slate-500 leading-snug mt-0.5">{{ t.message }}</p>
          </div>
          <button @click="quitarToast(t.id)" class="opacity-40 hover:opacity-100 transition-opacity">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
          <div
            :class="['toast-progress absolute bottom-0 left-0 h-1', TOAST_VARIANTS[t.type].bar]"
            :style="{ animationDuration: t.duracion + 'ms' }"
          ></div>
        </div>
      </TransitionGroup>
    </div>

    <!-- Restricción de Permiso General -->
    <div v-if="!can('USUARIOS', 'consultar')" class="bg-white rounded-3xl p-14 text-center shadow-md border border-slate-100">
      <div class="w-16 h-16 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4 ring-8 ring-rose-50/60">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
        </svg>
      </div>
      <h3 class="text-base font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-sm text-slate-500 mt-1.5 max-w-sm mx-auto">No cuentas con autorización para consultar la bitácora de auditoría y eventos forenses.</p>
    </div>

    <!-- Contenedor Principal de Bitácora -->
    <div v-else class="space-y-6">
      <!-- Encabezado con Indicador de Auditoría -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Bitácora de Auditoría del Sistema</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Trazabilidad inmutable de inserciones, cambios de estado, inicios de sesión e IPs de origen</p>
        </div>
        <div class="inline-flex items-center gap-2 px-3 py-1.5 rounded-xl bg-purple-50 border border-purple-200 text-purple-700 text-xs font-bold font-mono">
          <span class="w-2 h-2 rounded-full bg-purple-600 animate-pulse"></span>
          <span>Eventos en Memoria: {{ logs.length }}</span>
        </div>
      </div>

      <!-- Tarjetas KPIs Analíticas -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Eventos Filtrados</p>
            <p class="text-2xl font-black text-slate-900 mt-1 font-mono">{{ kpis.total }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center border border-purple-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Accesos (LOGIN)</p>
            <p class="text-2xl font-black text-sky-600 mt-1 font-mono">{{ kpis.logins }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-sky-50 text-sky-600 flex items-center justify-center border border-sky-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 16l-4-4m0 0l4-4m-4 4h14m-5 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h7a3 3 0 013 3v1" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Modificaciones</p>
            <p class="text-2xl font-black text-amber-600 mt-1 font-mono">{{ kpis.cambios }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center border border-amber-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Altas / Creaciones</p>
            <p class="text-2xl font-black text-emerald-600 mt-1 font-mono">{{ kpis.inserciones }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center border border-emerald-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" /></svg>
          </div>
        </div>
      </div>

      <!-- Barra de Filtros Avanzada -->
      <div class="bg-white rounded-2xl p-5 shadow-md border border-slate-100 space-y-3">
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-12 gap-3 items-end">
          <!-- Búsqueda General -->
          <div class="lg:col-span-4">
            <label class="block text-[11px] font-bold text-slate-700 uppercase tracking-wider mb-1.5">
              Usuario / Tabla / ID
            </label>
            <div class="relative">
              <input 
                v-model="filtros.busqueda" 
                @keyup.enter="cargarBitacora"
                type="text" 
                placeholder="Ej: Kenph, Usuarios, #8..." 
                class="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition"
              />
              <svg class="w-4 h-4 text-slate-400 absolute left-3 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" /></svg>
            </div>
          </div>

          <!-- Filtro Acción -->
          <div class="lg:col-span-3">
            <label class="block text-[11px] font-bold text-slate-700 uppercase tracking-wider mb-1.5">
              Tipo de Acción
            </label>
            <select 
              v-model="filtros.accion" 
              @change="cargarBitacora"
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-700 outline-none focus:bg-white focus:border-purple-500"
            >
              <option value="">Todas las acciones</option>
              <option value="LOGIN">LOGIN (Acceso al Sistema)</option>
              <option value="INSERT">INSERT (Inserción)</option>
              <option value="UPDATE">UPDATE (Modificación)</option>
              <option value="STATE_CHANGE">STATE_CHANGE (Cambio Estado)</option>
              <option value="UPDATE_PERMISOS">UPDATE_PERMISOS (Privilegios)</option>
              <option value="RECEPCION_COMPRA">RECEPCION_COMPRA (Kardex)</option>
              <option value="RESET_SOLICITUD">RESET_SOLICITUD (Seguridad)</option>
              <option value="RESET_EXITOSO">RESET_EXITOSO (Contraseña)</option>
              <option value="DELETE">DELETE (Baja)</option>
            </select>
          </div>

          <!-- Fecha Inicio -->
          <div class="lg:col-span-2">
            <label class="block text-[11px] font-bold text-slate-700 uppercase tracking-wider mb-1.5">
              Desde
            </label>
            <input 
              v-model="filtros.fechaInicio" 
              @change="cargarBitacora"
              type="date" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 outline-none focus:bg-white focus:border-purple-500"
            />
          </div>

          <!-- Fecha Fin -->
          <div class="lg:col-span-2">
            <label class="block text-[11px] font-bold text-slate-700 uppercase tracking-wider mb-1.5">
              Hasta
            </label>
            <input 
              v-model="filtros.fechaFin" 
              @change="cargarBitacora"
              type="date" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 outline-none focus:bg-white focus:border-purple-500"
            />
          </div>

          <!-- Botones de Acción -->
          <div class="lg:col-span-1 flex items-center gap-1.5">
            <button 
              @click="cargarBitacora" 
              class="w-full py-2 bg-gradient-to-tl from-purple-700 to-pink-500 hover:opacity-95 text-white font-bold text-xs rounded-xl shadow-md shadow-purple-500/25 transition"
              title="Ejecutar búsqueda"
            >
              Buscar
            </button>
          </div>
        </div>

        <div v-if="filtros.busqueda || filtros.accion || filtros.fechaInicio || filtros.fechaFin" class="flex justify-end pt-2 border-t border-slate-100">
          <button 
            @click="limpiarFiltros"
            class="text-xs font-bold text-purple-700 hover:text-purple-900 transition flex items-center gap-1"
          >
            <span>✕ Limpiar todos los filtros</span>
          </button>
        </div>
      </div>

      <!-- Tabla de Eventos de Auditoría -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1000px] text-left border-collapse">
            <thead>
              <tr class="bg-slate-50/70 border-b border-slate-100 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                <th class="py-3.5 px-6 whitespace-nowrap min-w-[190px]">Fecha y Hora (Local)</th>
                <th class="py-3.5 px-4 whitespace-nowrap min-w-[170px]">Usuario Responsable</th>
                <th class="py-3.5 px-4 whitespace-nowrap min-w-[180px]">Módulo del Sistema</th>
                <th class="py-3.5 px-4 whitespace-nowrap min-w-[140px]">Tabla / Registro</th>
                <th class="py-3.5 px-4 text-center whitespace-nowrap min-w-[150px]">Acción Ejecutada</th>
                <th class="py-3.5 px-4 text-center whitespace-nowrap min-w-[120px]">IP Origen</th>
                <th class="py-3.5 px-6 text-right whitespace-nowrap min-w-[130px]">Inspección</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-if="isLoading">
                <td colspan="7" class="py-14 text-center text-slate-400 font-semibold">
                  Cargando trazabilidad de eventos...
                </td>
              </tr>

              <tr 
                v-for="log in logs" 
                :key="log.idBitacora" 
                class="hover:bg-purple-50/20 transition-colors"
              >
                <!-- Fecha y Hora -->
                <td class="py-3.5 px-6 whitespace-nowrap">
                  <span class="block text-slate-900 font-bold font-mono text-[11px]">
                    {{ formatFecha(log.fechaRegistro) }}
                  </span>
                  <span class="text-[10px] text-slate-400 font-mono">Evento #{{ log.idBitacora }}</span>
                </td>

                <!-- Usuario -->
                <td class="py-3.5 px-4 whitespace-nowrap">
                  <div class="flex items-center gap-2.5">
                    <div class="w-8 h-8 rounded-xl bg-purple-100 text-purple-700 flex items-center justify-center font-bold text-[11px] uppercase border border-purple-200 shrink-0">
                      {{ log.nombreUsuario?.slice(0, 2) || 'US' }}
                    </div>
                    <div>
                      <span class="block text-slate-800 font-bold leading-tight">{{ log.nombreUsuario }}</span>
                      <span class="text-[10px] text-slate-400 font-mono">ID: {{ log.idUsuario }}</span>
                    </div>
                  </div>
                </td>

                <!-- Módulo -->
                <td class="py-3.5 px-4 whitespace-nowrap">
                  <span class="px-2.5 py-1 rounded-lg bg-slate-100 text-slate-700 font-bold text-[11px] border border-slate-200/80">
                    {{ log.modulo }}
                  </span>
                </td>

                <!-- Tabla / ID Afectado -->
                <td class="py-3.5 px-4 whitespace-nowrap">
                  <div class="font-mono text-xs">
                    <span class="font-bold text-slate-800">{{ log.tablaAfectada }}</span>
                    <span class="ml-1 text-[11px] font-black text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-200">
                      #{{ log.idRegistro }}
                    </span>
                  </div>
                </td>

                <!-- Badge de Acción -->
                <td class="py-3.5 px-4 text-center whitespace-nowrap">
                  <span :class="[
                    'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-2xs',
                    getAccionBadge(log.accion).badge
                  ]">
                    <span :class="['w-1.5 h-1.5 rounded-full', getAccionBadge(log.accion).dot]"></span>
                    {{ log.accion }}
                  </span>
                </td>

                <!-- IP de Origen -->
                <td class="py-3.5 px-4 text-center whitespace-nowrap">
                  <span class="px-2 py-0.5 rounded-md bg-slate-50 border border-slate-200 text-slate-600 font-mono text-[11px] font-bold">
                    {{ log.direccionIp || '127.0.0.1' }}
                  </span>
                </td>

                <!-- Botón de Inspección de Payload -->
                <td class="py-3.5 px-6 text-right whitespace-nowrap">
                  <button 
                    @click="abrirDetalle(log)"
                    class="px-2.5 py-1.5 bg-slate-100 hover:bg-purple-600 hover:text-white text-slate-700 rounded-lg text-xs font-bold transition-all border border-slate-200 shadow-xs inline-flex items-center gap-1.5"
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
                <td colspan="7" class="py-14 text-center">
                  <div class="w-14 h-14 bg-purple-50 text-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-3 border border-purple-100">
                    <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
                  </div>
                  <p class="text-sm font-black text-slate-800">No se encontraron eventos registrados</p>
                  <p class="text-xs text-slate-500 font-medium mt-1">Ajusta los filtros temporales o limpia los términos de búsqueda.</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Forense de Inspección de Registro (Diff y Raw) -->
    <Transition name="fade">
      <div v-if="showDetailModal" class="fixed inset-0 z-[75] flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="bg-white rounded-3xl shadow-2xl w-full max-w-3xl border border-slate-100 max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <!-- Header Modal Forense -->
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <div class="flex items-center gap-2">
                <h3 class="font-black text-slate-900 text-base">
                  Inspección Forense #{{ selectedLog?.idBitacora }}
                </h3>
                <span :class="[
                  'px-2.5 py-0.5 rounded-full text-[10px] font-black uppercase tracking-wider border',
                  getAccionBadge(selectedLog?.accion).badge
                ]">
                  {{ selectedLog?.accion }}
                </span>
              </div>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">
                {{ selectedLog?.modulo }} · Tabla: <span class="font-mono text-purple-700 font-bold">{{ selectedLog?.tablaAfectada }} #{{ selectedLog?.idRegistro }}</span>
              </p>
            </div>
            <button @click="showDetailModal = false" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <!-- Ficha de Metadatos de la Petición -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-2.5 px-6 py-3 bg-purple-50/40 border-b border-purple-100/60 text-xs font-mono">
            <div>
              <span class="text-[10px] uppercase font-bold text-slate-400 block">Usuario:</span>
              <span class="font-bold text-slate-800">{{ selectedLog?.nombreUsuario }}</span>
            </div>
            <div>
              <span class="text-[10px] uppercase font-bold text-slate-400 block">IP Registrada:</span>
              <span class="font-bold text-purple-700">{{ selectedLog?.direccionIp || '127.0.0.1' }}</span>
            </div>
            <div>
              <span class="text-[10px] uppercase font-bold text-slate-400 block">Fecha y Hora:</span>
              <span class="font-bold text-slate-800">{{ formatFecha(selectedLog?.fechaRegistro) }}</span>
            </div>
            <div class="flex items-center justify-end">
              <button
                @click="copiarJsonAlPortapapeles"
                class="px-2.5 py-1 rounded-lg text-[11px] font-bold bg-white text-purple-700 border border-purple-200 hover:bg-purple-100 transition shadow-2xs flex items-center gap-1"
              >
                <span>{{ copiadoExitoso ? '✓ Copiado' : '📋 Copiar JSON' }}</span>
              </button>
            </div>
          </div>

          <!-- Selector de Pestañas: Visual vs Raw -->
          <div class="flex items-center gap-2 px-6 pt-3 border-b border-slate-100">
            <button
              @click="tabModal = 'visual'"
              :class="[
                'pb-2.5 text-xs font-bold transition-all border-b-2',
                tabModal === 'visual'
                  ? 'border-purple-600 text-purple-700'
                  : 'border-transparent text-slate-400 hover:text-slate-700'
              ]"
            >
              📊 Comparación Visual (Diff)
            </button>
            <button
              @click="tabModal = 'raw'"
              :class="[
                'pb-2.5 text-xs font-bold transition-all border-b-2',
                tabModal === 'raw'
                  ? 'border-purple-600 text-purple-700'
                  : 'border-transparent text-slate-400 hover:text-slate-700'
              ]"
            >
              💻 Payload Raw (Terminal JSON)
            </button>
          </div>

          <!-- Contenido de las Pestañas -->
          <div class="p-6 overflow-y-auto flex-1">
            <!-- PESTAÑA 1: VISTA VISUAL COMPARATIVA -->
            <div v-if="tabModal === 'visual'" class="space-y-3">
              <div v-if="parsedDiff.length === 0" class="text-center py-10 text-xs text-slate-400 italic">
                No hay diferencias estructuradas registradas para este evento.
              </div>

              <div v-else class="overflow-x-auto border border-slate-200 rounded-2xl">
                <table class="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr class="bg-slate-50 text-[10px] font-bold text-slate-500 uppercase tracking-wider border-b border-slate-200">
                      <th class="py-2.5 px-3">Propiedad / Campo</th>
                      <th class="py-2.5 px-3">Estado Anterior</th>
                      <th class="py-2.5 px-3">Estado Nuevo</th>
                      <th class="py-2.5 px-3 text-center">Cambio</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-100">
                    <tr v-for="d in parsedDiff" :key="d.campo" class="hover:bg-purple-50/20">
                      <td class="py-2.5 px-3 font-bold text-slate-800 font-mono text-[11px]">
                        {{ d.campo }}
                      </td>
                      <td class="py-2.5 px-3 text-slate-500 font-mono text-[11px] max-w-xs break-words">
                        <span v-if="d.anterior !== null">{{ d.anterior }}</span>
                        <span v-else class="text-slate-300 italic">// Null / Nuevo</span>
                      </td>
                      <td class="py-2.5 px-3 font-mono font-bold text-[11px] max-w-xs break-words" :class="d.haCambiado ? 'text-purple-700' : 'text-slate-700'">
                        <span v-if="d.nuevo !== null">{{ d.nuevo }}</span>
                        <span v-else class="text-slate-300 italic">// Null / Vaciado</span>
                      </td>
                      <td class="py-2.5 px-3 text-center">
                        <span
                          v-if="d.haCambiado"
                          class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-purple-100 text-purple-700 border border-purple-200"
                        >
                          Modificado
                        </span>
                        <span v-else class="text-[10px] font-semibold text-slate-400">
                          Sin cambio
                        </span>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <!-- PESTAÑA 2: VISTA RAW TERMINAL -->
            <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <h4 class="text-xs font-bold text-slate-700 mb-1.5 uppercase tracking-wider flex items-center gap-1.5">
                  <span class="w-2 h-2 rounded-full bg-rose-500"></span>
                  Valores Anteriores (JSON)
                </h4>
                <pre class="p-3.5 bg-slate-900 rounded-2xl text-emerald-400 font-mono text-[11px] overflow-x-auto min-h-[180px] max-h-[300px] whitespace-pre-wrap border border-slate-800">{{ selectedLog?.valoresAnteriores ? JSON.stringify(JSON.parse(selectedLog.valoresAnteriores), null, 2) : '// Sin datos previos (Nuevo registro)' }}</pre>
              </div>

              <div>
                <h4 class="text-xs font-bold text-slate-700 mb-1.5 uppercase tracking-wider flex items-center gap-1.5">
                  <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
                  Valores Nuevos (JSON)
                </h4>
                <pre class="p-3.5 bg-slate-900 rounded-2xl text-purple-300 font-mono text-[11px] overflow-x-auto min-h-[180px] max-h-[300px] whitespace-pre-wrap border border-slate-800">{{ selectedLog?.valoresNuevos ? JSON.stringify(JSON.parse(selectedLog.valoresNuevos), null, 2) : '// Sin cambios registrados' }}</pre>
              </div>
            </div>
          </div>

          <!-- Footer Modal -->
          <div class="px-6 py-3.5 border-t border-slate-100 flex justify-end bg-slate-50/50">
            <button 
              @click="showDetailModal = false" 
              class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition"
            >
              Cerrar Inspección
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </MainLayout>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.toast-enter-active,
.toast-leave-active {
  transition: all 0.3s ease;
}
.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateX(30px);
}
.toast-move {
  transition: transform 0.3s ease;
}
.toast-progress {
  width: 100%;
  transform-origin: left;
  animation-name: toast-progress;
  animation-timing-function: linear;
  animation-fill-mode: forwards;
}
@keyframes toast-progress {
  from { transform: scaleX(1); }
  to { transform: scaleX(0); }
}

@keyframes scaleIn {
  from { transform: scale(0.96); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
}
.animate-scale-in {
  animation: scaleIn 0.18s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}
</style>