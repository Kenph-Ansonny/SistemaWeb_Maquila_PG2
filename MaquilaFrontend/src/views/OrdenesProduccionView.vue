<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import { ordenProduccionService } from '../services/ordenProduccionService'
import { usePermissions } from '../composables/usePermissions'

const { can } = usePermissions()

const SECUENCIA_ESTADOS = ['Iniciada', 'En Corte', 'En Costura', 'Terminada']

const ESTADO_STYLES = {
  'Iniciada': 'bg-slate-100 text-slate-600',
  'En Corte': 'bg-amber-100 text-amber-700',
  'En Costura': 'bg-indigo-100 text-indigo-700',
  'Terminada': 'bg-emerald-100 text-emerald-700'
}

// ---------- Estado base ----------
const ordenes = ref([])
const loading = ref(true)
const errorCarga = ref('')

const filtroEstado = ref('')
const busqueda = ref('')
let busquedaTimeout = null

// ---------- Modal: Nueva orden ----------
const showNuevaOrden = ref(false)
const pedidosDisponibles = ref([])
const cargandoPedidos = ref(false)
const isSaving = ref(false)
const formNueva = reactive({
  idPedidoDetalle: '',
  fechaInicio: new Date().toISOString().slice(0, 10),
  cantidadProgramada: null
})
const erroresNueva = reactive({})

const detalleSeleccionado = computed(() =>
  pedidosDisponibles.value.find(p => String(p.idPedidoDetalle) === String(formNueva.idPedidoDetalle)) || null
)

// ---------- Modal: Registrar avance ----------
const showAvance = ref(false)
const ordenSeleccionada = ref(null)
const formAvance = reactive({
  estadoOrden: '',
  cantidadProducida: null
})
const erroresAvance = reactive({})
const contextoPedido = ref(null)
const cargandoContexto = ref(false)

// Etapa desde la que se abrió el modal (para saber si el usuario está
// avanzando a una etapa nueva o solo corrigiendo la etapa actual).
const etapaOriginal = ref('')

// ---------- Carga de datos ----------
const cargarOrdenes = async () => {
  if (!can('PRODUCCION', 'consultar')) {
    loading.value = false
    return
  }

  loading.value = true
  errorCarga.value = ''
  try {
    ordenes.value = await ordenProduccionService.obtenerOrdenes({
      estado: filtroEstado.value || undefined,
      busqueda: busqueda.value || undefined
    })
  } catch (e) {
    errorCarga.value = e?.response?.data?.message || 'No se pudieron cargar las órdenes de producción.'
  } finally {
    loading.value = false
  }
}

const onBusquedaInput = () => {
  clearTimeout(busquedaTimeout)
  busquedaTimeout = setTimeout(cargarOrdenes, 350)
}

const setFiltroEstado = (estado) => {
  filtroEstado.value = estado
  cargarOrdenes()
}

onMounted(cargarOrdenes)

// ---------- KPIs ----------
const kpis = computed(() => {
  const lista = ordenes.value
  return {
    total: lista.length,
    iniciadas: lista.filter(o => o.estadoOrden === 'Iniciada').length,
    enProceso: lista.filter(o => o.estadoOrden === 'En Corte' || o.estadoOrden === 'En Costura').length,
    terminadas: lista.filter(o => o.estadoOrden === 'Terminada').length
  }
})

// ---------- Utilidades ----------
const formatFecha = (valor) => {
  if (!valor) return '—'
  return new Date(valor).toLocaleDateString('es-GT', { day: '2-digit', month: 'short', year: 'numeric' })
}

const porcentaje = (orden) => {
  if (!orden.cantidadProgramada) return 0
  return Math.min(100, Math.round((orden.cantidadProducida / orden.cantidadProgramada) * 100))
}

const puedeEliminar = (orden) => orden.estadoOrden === 'Iniciada' && orden.cantidadProducida === 0

// ---------- Nueva orden ----------
const abrirNuevaOrden = async () => {
  showNuevaOrden.value = true
  formNueva.idPedidoDetalle = ''
  formNueva.fechaInicio = new Date().toISOString().slice(0, 10)
  formNueva.cantidadProgramada = null
  Object.keys(erroresNueva).forEach(k => delete erroresNueva[k])

  cargandoPedidos.value = true
  try {
    pedidosDisponibles.value = await ordenProduccionService.obtenerPedidosDisponibles()
  } catch (e) {
    erroresNueva.general = e?.response?.data?.message || 'No se pudieron cargar los pedidos pendientes de programar.'
  } finally {
    cargandoPedidos.value = false
  }
}

const cerrarNuevaOrden = () => {
  if (isSaving.value) return
  showNuevaOrden.value = false
}

const validarNuevaOrden = () => {
  Object.keys(erroresNueva).forEach(k => delete erroresNueva[k])

  if (!formNueva.idPedidoDetalle) {
    erroresNueva.idPedidoDetalle = 'Selecciona el pedido a programar.'
  }
  if (!formNueva.fechaInicio) {
    erroresNueva.fechaInicio = 'Indica la fecha de inicio.'
  }
  const cantidad = Number(formNueva.cantidadProgramada)
  if (!cantidad || cantidad <= 0) {
    erroresNueva.cantidadProgramada = 'La cantidad debe ser mayor a cero.'
  } else if (detalleSeleccionado.value && cantidad > detalleSeleccionado.value.cantidadPendiente) {
    erroresNueva.cantidadProgramada = `No puede superar el saldo pendiente (${detalleSeleccionado.value.cantidadPendiente}).`
  }

  return Object.keys(erroresNueva).length === 0
}

const submitNuevaOrden = async () => {
  if (isSaving.value) return
  if (!validarNuevaOrden()) return

  isSaving.value = true
  try {
    await ordenProduccionService.crearOrden({
      idPedidoDetalle: Number(formNueva.idPedidoDetalle),
      fechaInicio: formNueva.fechaInicio,
      cantidadProgramada: Number(formNueva.cantidadProgramada)
    })
    showNuevaOrden.value = false
    await cargarOrdenes()
  } catch (e) {
    erroresNueva.general = e?.response?.data?.message || 'Ocurrió un error al crear la orden.'
  } finally {
    isSaving.value = false
  }
}

// ---------- Registrar avance ----------
const siguienteEstadoDisponible = computed(() => {
  if (!ordenSeleccionada.value) return []
  const idx = SECUENCIA_ESTADOS.indexOf(ordenSeleccionada.value.estadoOrden)
  const opciones = [SECUENCIA_ESTADOS[idx]]
  if (idx + 1 < SECUENCIA_ESTADOS.length) opciones.push(SECUENCIA_ESTADOS[idx + 1])
  return opciones
})

const abrirAvance = async (orden) => {
  ordenSeleccionada.value = orden
  etapaOriginal.value = orden.estadoOrden
  formAvance.estadoOrden = orden.estadoOrden
  formAvance.cantidadProducida = orden.cantidadProducida
  Object.keys(erroresAvance).forEach(k => delete erroresAvance[k])
  showAvance.value = true

  contextoPedido.value = null
  cargandoContexto.value = true
  try {
    contextoPedido.value = await ordenProduccionService.obtenerContextoPedido(orden.idPedido)
  } catch (e) {
    // El contexto es informativo: si falla, el formulario de avance igual funciona.
    contextoPedido.value = null
  } finally {
    cargandoContexto.value = false
  }
}

const cerrarAvance = () => {
  if (isSaving.value) return
  showAvance.value = false
  ordenSeleccionada.value = null
  contextoPedido.value = null
}

// Al elegir una etapa distinta a la actual, la cantidad se reinicia a 0:
// "cantidad producida" representa cuánto se lleva en ESA etapa, no un acumulado
// de todas las etapas anteriores, así que no tiene sentido arrastrar el número previo.
const seleccionarEtapa = (opcion) => {
  formAvance.estadoOrden = opcion
  if (opcion !== etapaOriginal.value) {
    formAvance.cantidadProducida = 0
  } else {
    formAvance.cantidadProducida = ordenSeleccionada.value.cantidadProducida
  }
}

const validarAvance = () => {
  Object.keys(erroresAvance).forEach(k => delete erroresAvance[k])
  const orden = ordenSeleccionada.value
  const cantidad = Number(formAvance.cantidadProducida)

  if (!formAvance.estadoOrden) {
    erroresAvance.estadoOrden = 'Selecciona la etapa.'
  }
  if (cantidad === null || cantidad === undefined || isNaN(cantidad) || cantidad < 0) {
    erroresAvance.cantidadProducida = 'Ingresa una cantidad válida.'
  } else if (cantidad > orden.cantidadProgramada) {
    erroresAvance.cantidadProducida = `No puede exceder lo programado (${orden.cantidadProgramada}).`
  }

  return Object.keys(erroresAvance).length === 0
}

const submitAvance = async () => {
  if (isSaving.value) return
  if (!validarAvance()) return

  isSaving.value = true
  try {
    await ordenProduccionService.registrarAvance(ordenSeleccionada.value.idOrden, {
      estadoOrden: formAvance.estadoOrden,
      cantidadProducida: Number(formAvance.cantidadProducida)
    })
    showAvance.value = false
    ordenSeleccionada.value = null
    await cargarOrdenes()
  } catch (e) {
    erroresAvance.general = e?.response?.data?.message || 'Ocurrió un error al registrar el avance.'
  } finally {
    isSaving.value = false
  }
}

// ---------- Eliminar ----------
const eliminarOrden = async (orden) => {
  if (isSaving.value) return
  if (!confirm(`¿Eliminar la orden #${orden.idOrden} del pedido ${orden.numeroPedido}? Esta acción no se puede deshacer.`)) return

  isSaving.value = true
  try {
    await ordenProduccionService.eliminarOrden(orden.idOrden)
    await cargarOrdenes()
  } catch (e) {
    errorCarga.value = e?.response?.data?.message || 'No se pudo eliminar la orden.'
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <MainLayout>
    <!-- Restricción Permiso -->
    <div v-if="!can('PRODUCCION', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar las órdenes de producción.</p>
    </div>

    <!-- Vista Principal -->
    <div v-else class="space-y-6">
    <!-- Encabezado -->
    <div class="bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h2 class="text-lg font-black text-slate-800">Órdenes de Producción</h2>
        <p class="text-xs text-slate-400 font-medium mt-0.5">Control de corte, costura y avance de producción por pedido</p>
      </div>
      <button
        v-if="can('PRODUCCION', 'insertar')"
        @click="abrirNuevaOrden"
        class="inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 transition-all"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        Nueva Orden
      </button>
    </div>

    <!-- KPIs -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
        <p class="text-[10px] font-bold text-slate-400 uppercase tracking-wide">Total</p>
        <p class="text-2xl font-black text-slate-800 mt-1">{{ kpis.total }}</p>
      </div>
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
        <p class="text-[10px] font-bold text-slate-400 uppercase tracking-wide">Iniciadas</p>
        <p class="text-2xl font-black text-slate-600 mt-1">{{ kpis.iniciadas }}</p>
      </div>
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
        <p class="text-[10px] font-bold text-slate-400 uppercase tracking-wide">En Proceso</p>
        <p class="text-2xl font-black text-indigo-600 mt-1">{{ kpis.enProceso }}</p>
      </div>
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
        <p class="text-[10px] font-bold text-slate-400 uppercase tracking-wide">Terminadas</p>
        <p class="text-2xl font-black text-emerald-600 mt-1">{{ kpis.terminadas }}</p>
      </div>
    </div>

    <!-- Filtros -->
    <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex flex-col md:flex-row md:items-center gap-3 md:gap-4">
      <div class="relative flex-1">
        <input
          v-model="busqueda"
          @input="onBusquedaInput"
          type="text"
          placeholder="Buscar por pedido, cliente o artículo..."
          class="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition"
        />
        <svg class="w-4 h-4 text-slate-400 absolute left-3 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </div>

      <div class="flex flex-wrap gap-1.5">
        <button
          v-for="opcion in [{ v: '', l: 'Todas' }, { v: 'Iniciada', l: 'Iniciada' }, { v: 'En Corte', l: 'En Corte' }, { v: 'En Costura', l: 'En Costura' }, { v: 'Terminada', l: 'Terminada' }]"
          :key="opcion.v"
          @click="setFiltroEstado(opcion.v)"
          :class="[
            'px-3 py-1.5 rounded-lg text-[11px] font-bold transition-all',
            filtroEstado === opcion.v
              ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm'
              : 'bg-slate-50 text-slate-500 hover:bg-slate-100'
          ]"
        >
          {{ opcion.l }}
        </button>
      </div>
    </div>

    <!-- Mensaje de error general -->
    <div v-if="errorCarga" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold rounded-xl px-4 py-3">
      {{ errorCarga }}
    </div>

    <!-- Tabla -->
    <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
      <div v-if="loading" class="flex items-center justify-center py-16">
        <svg class="animate-spin w-6 h-6 text-purple-600" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"></path>
        </svg>
      </div>

      <div v-else-if="ordenes.length === 0" class="text-center py-16 px-4">
        <p class="text-sm font-bold text-slate-600">No hay órdenes de producción con estos filtros</p>
        <p class="text-xs text-slate-400 mt-1">Crea una nueva orden a partir de un pedido con saldo pendiente.</p>
      </div>

      <div v-else class="overflow-x-auto">
        <table class="w-full text-xs">
          <thead>
            <tr class="border-b border-slate-100 text-left">
              <th class="px-5 py-3 font-bold text-slate-400 uppercase tracking-wide text-[10px]">Pedido / Cliente</th>
              <th class="px-5 py-3 font-bold text-slate-400 uppercase tracking-wide text-[10px]">Artículo / Receta</th>
              <th class="px-5 py-3 font-bold text-slate-400 uppercase tracking-wide text-[10px]">Inicio</th>
              <th class="px-5 py-3 font-bold text-slate-400 uppercase tracking-wide text-[10px]">Progreso</th>
              <th class="px-5 py-3 font-bold text-slate-400 uppercase tracking-wide text-[10px]">Estado</th>
              <th class="px-5 py-3 font-bold text-slate-400 uppercase tracking-wide text-[10px] text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="orden in ordenes"
              :key="orden.idOrden"
              class="border-b border-slate-50 last:border-0 hover:bg-slate-50/60 transition-colors"
            >
              <td class="px-5 py-3.5">
                <p class="font-bold text-slate-700">{{ orden.numeroPedido }}</p>
                <p class="text-slate-400">{{ orden.nombreCliente }}</p>
              </td>
              <td class="px-5 py-3.5">
                <p class="font-semibold text-slate-700">{{ orden.nombreArticulo }}</p>
                <p class="text-slate-400">{{ orden.nombreReceta }}</p>
              </td>
              <td class="px-5 py-3.5 text-slate-600">{{ formatFecha(orden.fechaInicio) }}</td>
              <td class="px-5 py-3.5 min-w-[140px]">
                <div class="flex items-center gap-2">
                  <div class="flex-1 h-2 bg-slate-100 rounded-full overflow-hidden">
                    <div
                      class="h-full bg-gradient-to-r from-purple-600 to-pink-500 rounded-full transition-all"
                      :style="{ width: porcentaje(orden) + '%' }"
                    ></div>
                  </div>
                  <span class="text-slate-500 font-semibold whitespace-nowrap">{{ orden.cantidadProducida }}/{{ orden.cantidadProgramada }}</span>
                </div>
              </td>
              <td class="px-5 py-3.5">
                <span :class="['px-2.5 py-1 rounded-lg text-[10px] font-bold', ESTADO_STYLES[orden.estadoOrden]]">
                  {{ orden.estadoOrden }}
                </span>
              </td>
              <td class="px-5 py-3.5">
                <div class="flex items-center justify-end gap-1.5">
                  <button
                    v-if="can('PRODUCCION', 'modificar') && orden.estadoOrden !== 'Terminada'"
                    @click="abrirAvance(orden)"
                    class="px-2.5 py-1.5 rounded-lg bg-purple-50 text-purple-700 font-bold text-[10px] hover:bg-purple-100 transition-colors"
                  >
                    Avanzar
                  </button>
                  <button
                    v-if="can('PRODUCCION', 'eliminar') && puedeEliminar(orden)"
                    @click="eliminarOrden(orden)"
                    class="px-2.5 py-1.5 rounded-lg bg-rose-50 text-rose-600 font-bold text-[10px] hover:bg-rose-100 transition-colors"
                  >
                    Eliminar
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
    </div>

    <!-- Modal: Nueva Orden -->
    <Transition name="fade">
      <div v-if="showNuevaOrden" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
            <h3 class="font-black text-slate-800 text-sm">Nueva Orden de Producción</h3>
            <button @click="cerrarNuevaOrden" class="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="px-6 py-5 space-y-4 max-h-[70vh] overflow-y-auto">
            <div v-if="erroresNueva.general" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold rounded-xl px-3 py-2">
              {{ erroresNueva.general }}
            </div>

            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Pedido pendiente de programar</label>
              <select
                v-model="formNueva.idPedidoDetalle"
                class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
              >
                <option value="" disabled>{{ cargandoPedidos ? 'Cargando pedidos...' : 'Selecciona un pedido' }}</option>
                <option v-for="p in pedidosDisponibles" :key="p.idPedidoDetalle" :value="p.idPedidoDetalle">
                  {{ p.numeroPedido }} — {{ p.nombreCliente }} — {{ p.nombreArticulo }} (pend. {{ p.cantidadPendiente }})
                </option>
              </select>
              <p v-if="erroresNueva.idPedidoDetalle" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresNueva.idPedidoDetalle }}</p>
              <p v-if="!cargandoPedidos && pedidosDisponibles.length === 0" class="text-slate-400 text-[11px] mt-1">
                No hay pedidos con saldo pendiente de programar.
              </p>
            </div>

            <div v-if="detalleSeleccionado" class="bg-purple-50/60 border border-purple-100 rounded-xl px-4 py-3 text-[11px] text-slate-600 space-y-0.5">
              <p><span class="font-bold text-slate-700">Receta:</span> {{ detalleSeleccionado.nombreReceta }}</p>
              <p><span class="font-bold text-slate-700">Cantidad pedida:</span> {{ detalleSeleccionado.cantidadPedida }}</p>
              <p><span class="font-bold text-slate-700">Ya programado:</span> {{ detalleSeleccionado.cantidadYaProgramada }}</p>
              <p><span class="font-bold text-emerald-700">Saldo disponible:</span> {{ detalleSeleccionado.cantidadPendiente }}</p>
            </div>

            <!-- Lotes ya programados para esta prenda: así se ve qué existe antes de -->
            <!-- decidir cuánto programar ahora (programar una parte y el resto luego). -->
            <div v-if="detalleSeleccionado && detalleSeleccionado.ordenesExistentes?.length" class="space-y-1.5">
              <p class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Lotes ya programados</p>
              <div class="flex flex-wrap gap-1.5">
                <span
                  v-for="lote in detalleSeleccionado.ordenesExistentes"
                  :key="lote.idOrden"
                  :class="['px-2.5 py-1 rounded-lg text-[10px] font-bold', ESTADO_STYLES[lote.estadoOrden]]"
                >
                  Lote #{{ lote.idOrden }} · {{ lote.cantidadProgramada }} uds · {{ lote.estadoOrden }}
                </span>
              </div>
              <p class="text-slate-400 text-[11px]">
                Puedes programar solo una parte del saldo disponible ahora y crear otra orden más tarde para el resto.
              </p>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Fecha de inicio</label>
                <input
                  v-model="formNueva.fechaInicio"
                  type="date"
                  class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                />
                <p v-if="erroresNueva.fechaInicio" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresNueva.fechaInicio }}</p>
              </div>
              <div>
                <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Cantidad a programar</label>
                <input
                  v-model="formNueva.cantidadProgramada"
                  type="number"
                  min="1"
                  :max="detalleSeleccionado?.cantidadPendiente"
                  placeholder="0"
                  class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                />
                <p v-if="erroresNueva.cantidadProgramada" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresNueva.cantidadProgramada }}</p>
              </div>
            </div>
          </div>

          <div class="px-6 py-4 border-t border-slate-100 flex items-center justify-end gap-2">
            <button
              @click="cerrarNuevaOrden"
              :disabled="isSaving"
              class="px-4 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100 rounded-xl transition-colors disabled:opacity-50"
            >
              Cancelar
            </button>
            <button
              @click="submitNuevaOrden"
              :disabled="isSaving"
              class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 disabled:opacity-60 transition-all"
            >
              {{ isSaving ? 'Guardando...' : 'Crear Orden' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Modal: Registrar Avance -->
    <Transition name="fade">
      <div v-if="showAvance" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h3 class="font-black text-slate-800 text-sm">Registrar Avance</h3>
              <p v-if="ordenSeleccionada" class="text-[11px] text-slate-400 font-medium mt-0.5">
                Orden #{{ ordenSeleccionada.idOrden }} — {{ ordenSeleccionada.numeroPedido }}
              </p>
            </div>
            <button @click="cerrarAvance" class="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div v-if="ordenSeleccionada" class="px-6 py-5 space-y-4 max-h-[75vh] overflow-y-auto">
            <div v-if="erroresAvance.general" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold rounded-xl px-3 py-2">
              {{ erroresAvance.general }}
            </div>

            <!-- Prenda que se está avanzando: resaltada para que no haya duda de "cuál es cuál" -->
            <div class="bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl px-4 py-3 text-white">
              <p class="text-[10px] font-bold uppercase tracking-wide text-white/70">Esta orden corresponde a</p>
              <p class="font-black text-sm mt-0.5">{{ ordenSeleccionada.nombreArticulo }}</p>
              <p class="text-[11px] text-white/80">{{ ordenSeleccionada.nombreReceta }} · {{ ordenSeleccionada.nombreCliente }}</p>
            </div>

            <!-- Contexto: el resto de prendas del mismo pedido, para ubicar esta orden entre todas -->
            <div v-if="contextoPedido && contextoPedido.prendas.length > 1" class="space-y-1.5">
              <p class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Otras prendas de este pedido</p>
              <div class="border border-slate-100 rounded-xl divide-y divide-slate-50 overflow-hidden">
                <div
                  v-for="prenda in contextoPedido.prendas"
                  :key="prenda.idPedidoDetalle"
                  :class="[
                    'px-3 py-2 flex items-center justify-between gap-2 text-[11px]',
                    prenda.idArticuloPrenda === ordenSeleccionada.idArticuloPrenda ? 'bg-purple-50/60' : ''
                  ]"
                >
                  <div>
                    <p class="font-bold text-slate-700">{{ prenda.nombreArticulo }}</p>
                    <p class="text-slate-400">{{ prenda.cantidadProducidaTotal }}/{{ prenda.cantidadProgramadaTotal }} producidas · {{ prenda.cantidadPedida }} pedidas</p>
                  </div>
                  <div class="flex flex-wrap gap-1 justify-end max-w-[55%]">
                    <span
                      v-for="lote in prenda.lotes"
                      :key="lote.idOrden"
                      :class="[
                        'px-1.5 py-0.5 rounded text-[9px] font-bold whitespace-nowrap',
                        lote.idOrden === ordenSeleccionada.idOrden ? 'ring-2 ring-purple-400' : '',
                        ESTADO_STYLES[lote.estadoOrden]
                      ]"
                    >
                      #{{ lote.idOrden }} {{ lote.estadoOrden }}
                    </span>
                  </div>
                </div>
              </div>
            </div>
            <p v-else-if="cargandoContexto" class="text-[11px] text-slate-400">Cargando contexto del pedido...</p>

            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Etapa</label>
              <div class="mt-1.5 flex gap-2">
                <button
                  v-for="opcion in siguienteEstadoDisponible"
                  :key="opcion"
                  @click="seleccionarEtapa(opcion)"
                  type="button"
                  :class="[
                    'flex-1 px-3 py-2 rounded-xl text-[11px] font-bold border transition-all',
                    formAvance.estadoOrden === opcion
                      ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white border-transparent shadow-sm'
                      : 'bg-white text-slate-500 border-slate-200 hover:bg-slate-50'
                  ]"
                >
                  {{ opcion }}
                </button>
              </div>
              <p v-if="erroresAvance.estadoOrden" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresAvance.estadoOrden }}</p>
              <p class="text-slate-400 text-[11px] mt-1">Solo puedes avanzar a la siguiente etapa, sin saltos ni retrocesos.</p>
            </div>

            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">
                Cantidad completada en "{{ formAvance.estadoOrden || ordenSeleccionada.estadoOrden }}" (de {{ ordenSeleccionada.cantidadProgramada }} programadas)
              </label>
              <input
                v-model="formAvance.cantidadProducida"
                type="number"
                min="0"
                :max="ordenSeleccionada.cantidadProgramada"
                class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
              />
              <p v-if="erroresAvance.cantidadProducida" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresAvance.cantidadProducida }}</p>
              <p v-else-if="formAvance.estadoOrden !== etapaOriginal" class="text-slate-400 text-[11px] mt-1">
                Estás entrando a una etapa nueva: el contador se reinició a 0, independiente de lo registrado en "{{ etapaOriginal }}".
              </p>

              <div class="mt-2 h-2 bg-slate-100 rounded-full overflow-hidden">
                <div
                  class="h-full bg-gradient-to-r from-purple-600 to-pink-500 rounded-full transition-all"
                  :style="{ width: Math.min(100, Math.round((Number(formAvance.cantidadProducida || 0) / ordenSeleccionada.cantidadProgramada) * 100)) + '%' }"
                ></div>
              </div>
            </div>

            <p v-if="formAvance.estadoOrden === 'Terminada'" class="text-[11px] font-semibold text-emerald-600 bg-emerald-50 rounded-xl px-3 py-2">
              Al guardar, esta orden quedará marcada como Terminada (se completan las {{ ordenSeleccionada.cantidadProgramada }} unidades programadas) y no admitirá más cambios.
              Si esto completa todas las prendas del pedido, el pedido pasará automáticamente a "Finalizado".
            </p>
          </div>

          <div class="px-6 py-4 border-t border-slate-100 flex items-center justify-end gap-2">
            <button
              @click="cerrarAvance"
              :disabled="isSaving"
              class="px-4 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100 rounded-xl transition-colors disabled:opacity-50"
            >
              Cancelar
            </button>
            <button
              @click="submitAvance"
              :disabled="isSaving"
              class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 disabled:opacity-60 transition-all"
            >
              {{ isSaving ? 'Guardando...' : 'Guardar Avance' }}
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
</style>