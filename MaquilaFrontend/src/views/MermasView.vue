<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import { ordenProduccionService } from '../services/ordenProduccionService'
import almacenService from '../services/almacenService'
import mermaService from '../services/mermaService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly } from '../utils/validators'

const { can } = usePermissions()

const TIPOS_MERMA = ['Retazo Aprovechable', 'Desperdicio Irrecuperable', 'Prenda Defectuosa']

const TIPO_STYLES = {
  'Retazo Aprovechable': 'bg-emerald-100 text-emerald-800 border-emerald-300',
  'Desperdicio Irrecuperable': 'bg-rose-100 text-rose-800 border-rose-300',
  'Prenda Defectuosa': 'bg-amber-100 text-amber-800 border-amber-300'
}

const TIPO_DOT = {
  'Retazo Aprovechable': 'bg-emerald-500',
  'Desperdicio Irrecuperable': 'bg-rose-500',
  'Prenda Defectuosa': 'bg-amber-500'
}

// ---------- Modal: Ver Detalle de Merma ----------
const showDetalleModal = ref(false)
const mermaDetalle = ref(null)

const verDetalle = (m) => {
  mermaDetalle.value = m
  showDetalleModal.value = true
}

// ---------- Diálogo de Confirmación Reutilizable (Pop-in) ----------
const CONFIRM_VARIANTS = {
  danger: {
    iconWrap: 'bg-rose-100 text-rose-600 ring-8 ring-rose-50',
    button: 'bg-gradient-to-tl from-rose-600 to-red-500 shadow-rose-500/30',
    panel: 'bg-rose-50/60 border-rose-100',
    bar: 'from-rose-500 to-red-400',
    icon: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z'
  },
  warning: {
    iconWrap: 'bg-amber-100 text-amber-600 ring-8 ring-amber-50',
    button: 'bg-gradient-to-tl from-amber-500 to-orange-500 shadow-amber-500/30',
    panel: 'bg-amber-50/60 border-amber-100',
    bar: 'from-amber-400 to-orange-500',
    icon: 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  primary: {
    iconWrap: 'bg-purple-100 text-purple-600 ring-8 ring-purple-50',
    button: 'bg-gradient-to-tl from-purple-700 to-pink-500 shadow-purple-500/30',
    panel: 'bg-purple-50/60 border-purple-100',
    bar: 'from-purple-600 to-pink-500',
    icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  info: {
    iconWrap: 'bg-sky-100 text-sky-600 ring-8 ring-sky-50',
    button: 'bg-gradient-to-tl from-sky-600 to-cyan-500 shadow-sky-500/30',
    panel: 'bg-sky-50/60 border-sky-100',
    bar: 'from-sky-500 to-cyan-400',
    icon: 'M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
  }
}

const confirmDialog = reactive({
  visible: false,
  variant: 'primary',
  title: '',
  message: '',
  detalles: [],
  confirmText: 'Confirmar',
  cancelText: 'Volver'
})
let confirmResolver = null
const confirmBtnRef = ref(null)
const cancelBtnRef = ref(null)

const pedirConfirmacion = (opciones) =>
  new Promise((resolve) => {
    Object.assign(confirmDialog, {
      variant: 'primary',
      title: '',
      message: '',
      detalles: [],
      confirmText: 'Confirmar',
      cancelText: 'Volver',
      ...opciones,
      visible: true
    })
    confirmResolver = resolve

    nextTick(() => {
      const destructiva = confirmDialog.variant === 'danger' || confirmDialog.variant === 'warning'
      const objetivo = destructiva ? cancelBtnRef.value : confirmBtnRef.value
      objetivo?.focus()
    })
  })

const resolverConfirmacion = (valor) => {
  confirmDialog.visible = false
  if (confirmResolver) confirmResolver(valor)
  confirmResolver = null
}

// ---------- Toasts Notificaciones ----------
const TOAST_VARIANTS = {
  success: {
    wrap: 'bg-white border-emerald-200 text-slate-700',
    iconWrap: 'bg-emerald-100 text-emerald-600',
    bar: 'bg-emerald-500',
    title: 'Completado',
    icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  error: {
    wrap: 'bg-white border-rose-200 text-slate-700',
    iconWrap: 'bg-rose-100 text-rose-600',
    bar: 'bg-rose-500',
    title: 'Atención requerida',
    icon: 'M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  warning: {
    wrap: 'bg-white border-amber-200 text-slate-700',
    iconWrap: 'bg-amber-100 text-amber-600',
    bar: 'bg-amber-500',
    title: 'Aviso',
    icon: 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  info: {
    wrap: 'bg-white border-sky-200 text-slate-700',
    iconWrap: 'bg-sky-100 text-sky-600',
    bar: 'bg-sky-500',
    title: 'Información',
    icon: 'M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
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

// ---------- Estado Base ----------
const mermas = ref([])
const ordenes = ref([])
const almacenes = ref([])
const insumosOrden = ref([])
const cargandoInsumos = ref(false)
const cargandoDatosBase = ref(false)
const isSaving = ref(false)
const isLoading = ref(true)

// ---------- Filtros ----------
const filtroBusqueda = ref('')
const filtroTipo = ref('')
let busquedaTimeout = null

// ---------- Modal Registro ----------
const showModal = ref(false)
const formMerma = reactive({
  idOrdenProduccion: '',
  idArticuloInsumo: '',
  cantidadMermaReal: null,
  tipoMerma: 'Retazo Aprovechable',
  idAlmacenDestino: '',
  observaciones: ''
})
const erroresForm = reactive({})

// ---------- Carga de Mermas ----------
const cargarMermas = async () => {
  if (!can('MERMAS', 'consultar')) {
    isLoading.value = false
    return
  }
  isLoading.value = true
  try {
    const data = await mermaService.obtenerTodos({
      tipoMerma: filtroTipo.value || undefined,
      busqueda: filtroBusqueda.value || undefined
    })
    mermas.value = Array.isArray(data) ? data : (data?.data || [])
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudieron obtener las mermas registradas.')
  } finally {
    isLoading.value = false
  }
}

// ---------- Carga de Órdenes y Almacenes
const cargarDatosBase = async () => {
  cargandoDatosBase.value = true
  try {
    const [resOrdenes, resAlmacenes] = await Promise.all([
      ordenProduccionService.obtenerOrdenes({ tamanioPagina: 100 }),
      almacenService.obtenerTodos()
    ])

    const rawOrdenes = Array.isArray(resOrdenes)
      ? resOrdenes
      : (resOrdenes?.items || resOrdenes?.data?.items || resOrdenes?.data || [])

    // Solo órdenes activas en mesa de corte o línea de costura
    ordenes.value = rawOrdenes.filter(o =>
      o.estadoOrden === 'En Corte' || o.estadoOrden === 'En Costura'
    )

    const rawAlmacenes = Array.isArray(resAlmacenes)
      ? resAlmacenes
      : (resAlmacenes?.data || resAlmacenes?.items || [])

    almacenes.value = rawAlmacenes.filter(a => a.estadoAlmacen ?? a.activo ?? true)
  } catch (err) {
    mostrarToast('error', 'No se pudieron sincronizar las órdenes o almacenes.')
  } finally {
    cargandoDatosBase.value = false
  }
}

const onBusquedaInput = () => {
  clearTimeout(busquedaTimeout)
  busquedaTimeout = setTimeout(cargarMermas, 350)
}

const setFiltroTipo = (tipo) => {
  filtroTipo.value = tipo
  cargarMermas()
}

// Control de teclado ESC
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) {
    resolverConfirmacion(false)
  } else if (showModal.value) {
    cerrarModal()
  }
}

onMounted(async () => {
  window.addEventListener('keydown', onKeydown)
  await cargarMermas()
  await cargarDatosBase()
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeydown)
  clearTimeout(busquedaTimeout)
})

// ---------- Computados ----------
const kpis = computed(() => {
  const lista = mermas.value
  return {
    total: lista.length,
    retazos: lista.filter(m => m.tipoMerma === 'Retazo Aprovechable').length,
    desperdicios: lista.filter(m => m.tipoMerma === 'Desperdicio Irrecuperable').length,
    defectuosas: lista.filter(m => m.tipoMerma === 'Prenda Defectuosa').length
  }
})

const ordenSeleccionadaInfo = computed(() => {
  if (!formMerma.idOrdenProduccion) return null
  return ordenes.value.find(o => o.idOrden === Number(formMerma.idOrdenProduccion)) || null
})

const insumoSeleccionadoInfo = computed(() => {
  if (!formMerma.idArticuloInsumo) return null
  return insumosOrden.value.find(i => i.idArticuloInsumo === Number(formMerma.idArticuloInsumo)) || null
})

// Formateo de fecha
const formatFecha = (valor) => {
  if (!valor) return '—'
  return new Date(valor).toLocaleDateString('es-GT', { 
    day: '2-digit', 
    month: 'short', 
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// ---------- Gestión del Formulario ----------
const onOrdenChange = async () => {
  formMerma.idArticuloInsumo = ''
  insumosOrden.value = []
  if (!formMerma.idOrdenProduccion) return

  cargandoInsumos.value = true
  try {
    const data = await mermaService.obtenerInsumosDeOrden(formMerma.idOrdenProduccion)
    insumosOrden.value = Array.isArray(data) ? data : (data?.data || [])
    if (insumosOrden.value.length > 0) {
      formMerma.idArticuloInsumo = insumosOrden.value[0].idArticuloInsumo
    }
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudieron consultar los insumos de esta orden.')
  } finally {
    cargandoInsumos.value = false
  }
}

const limpiarErrores = () => {
  Object.keys(erroresForm).forEach(k => delete erroresForm[k])
}

const abrirModalCrear = async () => {
  if (!can('MERMAS', 'insertar')) return
  limpiarErrores()

  if (ordenes.value.length === 0 || almacenes.value.length === 0) {
    await cargarDatosBase()
  }

  // Preselecciona la primera orden en corte/costura si existe, o la primera de la lista
  const ordenSugerida = ordenes.value.find(o => o.estadoOrden === 'En Corte' || o.estadoOrden === 'En Costura') || ordenes.value[0]

  formMerma.idOrdenProduccion = ordenSugerida?.idOrden || ''
  formMerma.idArticuloInsumo = ''
  formMerma.cantidadMermaReal = null
  formMerma.tipoMerma = 'Retazo Aprovechable'
  formMerma.idAlmacenDestino = almacenes.value[0]?.idAlmacen || ''
  formMerma.observaciones = ''

  showModal.value = true

  if (formMerma.idOrdenProduccion) {
    await onOrdenChange()
  }
}

const cerrarModal = () => {
  if (isSaving.value) return
  showModal.value = false
}

const validarFormulario = () => {
  limpiarErrores()
  if (!formMerma.idOrdenProduccion) {
    erroresForm.idOrdenProduccion = 'Selecciona la orden de producción.'
  }
  if (!formMerma.idArticuloInsumo) {
    erroresForm.idArticuloInsumo = 'Selecciona el insumo o material afectado.'
  }
  const cant = Number(formMerma.cantidadMermaReal)
  if (!cant || cant <= 0) {
    erroresForm.cantidadMermaReal = 'La cantidad física pesada debe ser mayor a cero.'
  }
  if (formMerma.tipoMerma === 'Retazo Aprovechable' && !formMerma.idAlmacenDestino) {
    erroresForm.idAlmacenDestino = 'Indica el almacén donde ingresará el retazo.'
  }
  return Object.keys(erroresForm).length === 0
}

const solicitarConfirmacionGuardar = async () => {
  if (!validarFormulario()) return

  const orden = ordenSeleccionadaInfo.value
  const insumo = insumoSeleccionadoInfo.value
  const esRetazo = formMerma.tipoMerma === 'Retazo Aprovechable'
  const almacen = almacenes.value.find(a => a.idAlmacen === Number(formMerma.idAlmacenDestino))

  const confirmado = await pedirConfirmacion({
    variant: esRetazo ? 'primary' : 'warning',
    title: '¿Confirmar registro de merma?',
    message: esRetazo
      ? 'La cantidad pesada sumará stock disponible de forma inmediata en el almacén de destino.'
      : 'Esta merma quedará registrada como desperdicio físico no recuperable para auditoría de taller.',
    detalles: [
      `Orden #${formMerma.idOrdenProduccion} (${orden?.numeroPedido || 'S/P'})`,
      `Insumo: ${insumo?.nombreInsumo || '—'}`,
      `Cantidad: ${formMerma.cantidadMermaReal} ${insumo?.unidadMedida || 'Uds'}`,
      `Clasificación: ${formMerma.tipoMerma}`,
      esRetazo ? `Bodega Destino: ${almacen?.nombreAlmacen}` : 'Destino: Desecho sin reingreso'
    ],
    confirmText: 'Sí, registrar merma',
    cancelText: 'Volver a revisar'
  })

  if (!confirmado) return

  isSaving.value = true
  const payload = {
    idOrdenProduccion: Number(formMerma.idOrdenProduccion),
    idArticuloInsumo: Number(formMerma.idArticuloInsumo),
    cantidadMermaReal: Number(formMerma.cantidadMermaReal),
    tipoMerma: formMerma.tipoMerma,
    idAlmacenDestino: esRetazo ? Number(formMerma.idAlmacenDestino) : null,
    observaciones: formMerma.observaciones?.trim() || null
  }

  try {
    await mermaService.crear(payload)
    showModal.value = false
    mostrarToast('success', 'Merma física asentada correctamente.')
    await cargarMermas()
  } catch (err) {
    const msg = err?.response?.data?.message || 'Ocurrió un error al registrar la merma.'
    erroresForm.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

// ---------- Anular Merma ----------
const solicitarEliminar = async (m) => {
  if (!can('MERMAS', 'eliminar')) return
  const esRetazo = m.tipoMerma === 'Retazo Aprovechable'

  const confirmado = await pedirConfirmacion({
    variant: 'danger',
    title: '¿Anular registro de merma?',
    message: esRetazo
      ? `Se anulará el registro #${m.idMerma} y se descontarán ${m.cantidadMermaReal} ${m.unidadMedida} de las existencias en "${m.nombreAlmacenDestino}".`
      : `Se anulará permanentemente el registro de merma #${m.idMerma} de la orden #${m.idOrdenProduccion}.`,
    detalles: [
      `Orden #${m.idOrdenProduccion} (${m.numeroPedido})`,
      `Insumo: ${m.nombreInsumo}`,
      `Cantidad: ${Number(m.cantidadMermaReal).toFixed(2)} ${m.unidadMedida}`
    ],
    confirmText: 'Sí, anular merma',
    cancelText: 'Cancelar'
  })

  if (!confirmado) return

  isSaving.value = true
  try {
    const res = await mermaService.eliminar(m.idMerma)
    mostrarToast('success', res?.message || res?.data?.message || 'Merma anulada y stock revertido.')
    await cargarMermas()
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo anular la merma.')
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <MainLayout>
    <!-- Restricción Permiso -->
    <div v-if="!can('MERMAS', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar el módulo de mermas.</p>
    </div>

    <!-- Vista Principal -->
    <div v-else class="space-y-6">
      <!-- Encabezado con franja gradiente -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-lg font-black text-slate-800">Control de Mermas de Confección</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de retazos reutilizables de corte, desperdicios y prendas defectuosas</p>
        </div>
        <button
          v-if="can('MERMAS', 'insertar')"
          @click="abrirModalCrear"
          class="inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:-translate-y-0.5 active:translate-y-0 transition-all focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/30 whitespace-nowrap"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Registrar Merma
        </button>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Total Registros</p>
            <p class="text-2xl font-black text-slate-800 mt-1 font-mono">{{ kpis.total }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-slate-100 text-slate-500 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-emerald-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-emerald-600 uppercase tracking-wide">Retazos Útiles</p>
            <p class="text-2xl font-black text-emerald-700 mt-1 font-mono">{{ kpis.retazos }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-emerald-100 text-emerald-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-rose-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-rose-500 uppercase tracking-wide">Desperdicios</p>
            <p class="text-2xl font-black text-rose-700 mt-1 font-mono">{{ kpis.desperdicios }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-rose-100 text-rose-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-amber-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-amber-600 uppercase tracking-wide">Defectuosas</p>
            <p class="text-2xl font-black text-amber-700 mt-1 font-mono">{{ kpis.defectuosas }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-amber-100 text-amber-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
          </div>
        </div>
      </div>

      <!-- Barra de Filtros -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex flex-col md:flex-row md:items-center gap-3 md:gap-4">
        <div class="relative flex-1">
          <input
            v-model="filtroBusqueda"
            @input="onBusquedaInput"
            type="text"
            placeholder="Buscar por pedido, tela, insumo o código..."
            class="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition"
          />
          <svg class="w-4 h-4 text-slate-400 absolute left-3 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>

        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="opcion in [{ v: '', l: 'Todas' }, ...TIPOS_MERMA.map(t => ({ v: t, l: t }))]"
            :key="opcion.v"
            @click="setFiltroTipo(opcion.v)"
            :class="[
              'px-3 py-1.5 rounded-lg text-[11px] font-bold transition-all',
              filtroTipo === opcion.v
                ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm shadow-purple-500/30'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            ]"
          >
            {{ opcion.l }}
          </button>
        </div>
      </div>

      <!-- Tabla de Datos -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <!-- Esqueleto de Carga -->
        <div v-if="isLoading" class="divide-y divide-slate-50" aria-busy="true">
          <div v-for="n in 5" :key="n" class="px-5 py-4 flex items-center gap-4 animate-pulse">
            <div class="flex-1 space-y-2">
              <div class="h-3 bg-slate-100 rounded w-28"></div>
              <div class="h-2.5 bg-slate-100 rounded w-40"></div>
            </div>
            <div class="flex-1 space-y-2 hidden sm:block">
              <div class="h-3 bg-slate-100 rounded w-32"></div>
              <div class="h-2.5 bg-slate-100 rounded w-20"></div>
            </div>
            <div class="h-6 bg-slate-100 rounded-lg w-28"></div>
            <div class="h-6 bg-slate-100 rounded-lg w-16"></div>
          </div>
        </div>

        <!-- Estado Vacío -->
        <div v-else-if="mermas.length === 0" class="text-center py-16 px-4">
          <div class="w-14 h-14 rounded-2xl bg-purple-50 text-purple-400 flex items-center justify-center mx-auto mb-3">
            <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" /></svg>
          </div>
          <p class="text-sm font-bold text-slate-600">No hay mermas registradas con estos filtros</p>
          <p class="text-xs text-slate-400 mt-1">Registra los sobrantes de corte o desperdicios generados en taller.</p>
        </div>

        <!-- Contenido de Tabla -->
        <div v-else class="overflow-x-auto">
          <table class="w-full text-xs">
            <thead>
              <tr class="border-b-2 border-slate-100 text-left bg-slate-50/60">
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Orden / Pedido</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Insumo / Tela Afectada</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Cantidad Pesada</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-center">Clasificación</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Destino de Reingreso</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Fecha Registro</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="m in mermas"
                :key="m.idMerma"
                class="border-b border-slate-50 last:border-0 hover:bg-purple-50/30 transition-colors"
              >
                <!-- Orden y Pedido -->
                <td class="px-5 py-3.5">
                  <p class="font-bold text-slate-700">Orden #{{ m.idOrdenProduccion }}</p>
                  <p class="text-slate-400">{{ m.numeroPedido }} · {{ m.nombrePrenda }}</p>
                </td>

                <!-- Insumo -->
                <td class="px-5 py-3.5">
                  <p class="font-semibold text-slate-700">{{ m.nombreInsumo }}</p>
                  <p class="text-slate-400 font-mono">{{ m.codigoInsumo }}</p>
                </td>

                <!-- Cantidad -->
                <td class="px-5 py-3.5 text-right font-mono font-bold text-slate-800 text-xs">
                  {{ Number(m.cantidadMermaReal).toFixed(2) }} {{ m.unidadMedida }}
                </td>

                <!-- Tipo -->
                <td class="px-5 py-3.5 text-center">
                  <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[10px] font-bold border', TIPO_STYLES[m.tipoMerma]]">
                    <span :class="['w-1.5 h-1.5 rounded-full', TIPO_DOT[m.tipoMerma]]"></span>
                    {{ m.tipoMerma }}
                  </span>
                </td>

                <!-- Destino -->
                <td class="px-5 py-3.5">
                  <div v-if="m.nombreAlmacenDestino" class="flex items-center gap-1.5 text-emerald-700 font-bold text-xs">
                    <svg class="w-3.5 h-3.5 text-emerald-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                    </svg>
                    <span>{{ m.nombreAlmacenDestino }}</span>
                  </div>
                  <span v-else class="text-slate-400 text-xs italic font-semibold">Desecho no reutilizable</span>
                </td>

                <!-- Fecha -->
                <td class="px-5 py-3.5 text-slate-600 font-medium whitespace-nowrap">
                  {{ formatFecha(m.fechaRegistro) }}
                </td>
                
                <!-- Columna Acciones en la tabla -->
                <td class="py-3.5 px-5 text-right">
                <div class="flex items-center justify-end gap-1.5">
                    <button
                    @click="verDetalle(m)"
                    class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-purple-700 bg-purple-50 hover:bg-purple-600 hover:text-white border border-purple-200 transition-all shadow-2xs"
                    >
                    Detalle
                    </button>
                    <button
                    v-if="can('MERMAS', 'eliminar')"
                    @click="solicitarEliminar(m)"
                    :disabled="isSaving"
                    class="px-2.5 py-1.5 rounded-lg bg-rose-50 text-rose-600 font-bold text-xs hover:bg-rose-600 hover:text-white border border-rose-200 transition-all disabled:opacity-50 shadow-2xs"
                    >
                    Anular
                    </button>
                </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Formulario: Registrar Merma Física -->
    <Transition name="fade">
      <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h3 class="font-black text-slate-800 text-sm">Registrar Merma Física</h3>
              <p class="text-[11px] text-slate-400 font-medium mt-0.5">Asienta sobrantes de corte o tela no utilizable vinculada a una orden</p>
            </div>
            <button @click="cerrarModal" class="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100" aria-label="Cerrar">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <form @submit.prevent="solicitarConfirmacionGuardar" class="px-6 py-5 space-y-4 max-h-[75vh] overflow-y-auto">
            <div v-if="erroresForm.general" class="flex items-start gap-2 bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold rounded-xl px-3 py-2" role="alert">
              <svg class="w-4 h-4 flex-shrink-0 mt-px" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              {{ erroresForm.general }}
            </div>

            <!-- Selector de Orden de Producción -->
            <div>
            <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">
                Orden de Producción en Taller <span class="text-rose-500">*</span>
            </label>
            <select
                v-model="formMerma.idOrdenProduccion"
                @change="onOrdenChange"
                required
                :disabled="cargandoDatosBase || ordenes.length === 0"
                class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 font-semibold focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 disabled:opacity-50"
            >
                <option value="" disabled>
                {{ cargandoDatosBase 
                    ? 'Cargando órdenes activas...' 
                    : (ordenes.length === 0 
                        ? 'No hay órdenes en corte o costura en este momento' 
                        : 'Selecciona una orden en proceso de corte o costura') }}
                </option>
                <option v-for="o in ordenes" :key="o.idOrden" :value="o.idOrden">
                Orden #{{ o.idOrden }} — {{ o.numeroPedido }} ({{ o.nombreArticulo || o.nombrePrenda }}) [{{ o.estadoOrden }}]
                </option>
            </select>
            <p v-if="erroresForm.idOrdenProduccion" class="text-rose-500 text-[11px] font-semibold mt-1">
                {{ erroresForm.idOrdenProduccion }}
            </p>
            </div>

            <!-- Selector de Insumo / Rollo de Tela -->
            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">
                Insumo / Material Consumido <span class="text-rose-500">*</span>
              </label>
              <select
                v-model="formMerma.idArticuloInsumo"
                :disabled="cargandoInsumos || insumosOrden.length === 0"
                required
                class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 font-semibold focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 disabled:opacity-50"
              >
                <option value="" disabled>{{ cargandoInsumos ? 'Consultando ficha técnica...' : (insumosOrden.length === 0 ? 'Sin insumos disponibles' : 'Selecciona el insumo afectado') }}</option>
                <option v-for="ins in insumosOrden" :key="ins.idArticuloInsumo" :value="ins.idArticuloInsumo">
                  {{ ins.nombreInsumo }} ({{ ins.codigoInsumo }}) — Merma teórica: {{ ins.porcentajeMermaEstimado }}%
                </option>
              </select>
              <p v-if="erroresForm.idArticuloInsumo" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresForm.idArticuloInsumo }}</p>

              <!-- Tarjeta Informativa de la Ficha Técnica (BOM) -->
              <div v-if="insumoSeleccionadoInfo" class="mt-2 p-3 bg-purple-50/60 border border-purple-100 rounded-xl text-[11px] text-slate-600 space-y-1">
                <div class="flex justify-between">
                  <span class="font-bold text-slate-700">Consumo bruto programado:</span>
                  <span class="font-mono font-bold">{{ Number(insumoSeleccionadoInfo.cantidadBrutaTeorica).toFixed(2) }} {{ insumoSeleccionadoInfo.unidadMedida }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="font-bold text-slate-700">Merma teórica estimada:</span>
                  <span class="font-mono font-bold text-purple-700">{{ Number(insumoSeleccionadoInfo.cantidadMermaEstimada).toFixed(2) }} {{ insumoSeleccionadoInfo.unidadMedida }} ({{ insumoSeleccionadoInfo.porcentajeMermaEstimado }}%)</span>
                </div>
                <div class="flex justify-between pt-1 border-t border-purple-200/60">
                  <span class="font-bold text-slate-700">Merma real ya pesada:</span>
                  <span class="font-mono font-bold text-amber-700">{{ Number(insumoSeleccionadoInfo.cantidadMermaYaRegistrada).toFixed(2) }} {{ insumoSeleccionadoInfo.unidadMedida }}</span>
                </div>
              </div>
            </div>

            <!-- Cantidad Pesada y Tipo de Merma -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">
                  Cantidad Física Pesada <span class="text-rose-500">*</span>
                </label>
                <div class="relative mt-1">
                  <input
                    v-model.number="formMerma.cantidadMermaReal"
                    type="number"
                    step="0.01"
                    min="0.01"
                    placeholder="0.00"
                    required
                    class="w-full pl-3 pr-12 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                  />
                  <span class="absolute right-3 top-2 text-xs font-bold text-slate-400 font-mono">
                    {{ insumoSeleccionadoInfo?.unidadMedida || 'Uds' }}
                  </span>
                </div>
                <p v-if="erroresForm.cantidadMermaReal" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresForm.cantidadMermaReal }}</p>
              </div>

              <div>
                <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">
                  Clasificación del Sobrante <span class="text-rose-500">*</span>
                </label>
                <select
                  v-model="formMerma.tipoMerma"
                  required
                  class="mt-1 w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                >
                  <option v-for="t in TIPOS_MERMA" :key="t" :value="t">{{ t }}</option>
                </select>
              </div>
            </div>

            <!-- Almacén Destino (Solo si es Retazo Aprovechable) -->
            <div v-if="formMerma.tipoMerma === 'Retazo Aprovechable'">
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">
                Bodega de Retazos Reutilizables <span class="text-rose-500">*</span>
              </label>
              <select
                v-model="formMerma.idAlmacenDestino"
                required
                class="mt-1 w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
              >
                <option value="" disabled>Selecciona el almacén de destino</option>
                <option v-for="a in almacenes" :key="a.idAlmacen" :value="a.idAlmacen">
                  {{ a.nombreAlmacen }}
                </option>
              </select>
              <p class="text-[10px] text-emerald-700 font-semibold mt-1">
                ✓ Esta cantidad se sumará inmediatamente al stock disponible de esta bodega.
              </p>
            </div>

            <div v-else class="p-3 bg-amber-50 border border-amber-200 rounded-xl text-[11px] text-amber-800 font-medium">
              ℹ Esta merma no reingresará al inventario. Quedará asentada para auditoría y evaluación del rendimiento de corte.
            </div>

            <!-- Observaciones -->
            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Observaciones / Motivo</label>
              <textarea
                v-model="formMerma.observaciones"
                @keypress="allowOnly.descripcionInput($event)"
                rows="2"
                maxlength="250"
                placeholder="Ej: Retazo aprovechable para confección de cuellos o vistas..."
                class="mt-1 w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
              ></textarea>
            </div>

            <div class="px-6 py-4 border-t border-slate-100 flex items-center justify-end gap-2 -mx-6 -mb-5 bg-slate-50/50">
              <button
                type="button"
                @click="cerrarModal"
                :disabled="isSaving"
                class="px-4 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100 rounded-xl transition-colors disabled:opacity-50"
              >
                Cancelar
              </button>
              <button
                type="submit"
                :disabled="isSaving"
                class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 disabled:opacity-60 transition-all hover:shadow-lg hover:-translate-y-0.5 active:translate-y-0"
              >
                Revisar y Asentar
              </button>
            </div>
          </form>
        </div>
      </div>
    </Transition>

    <!-- Diálogo de Confirmación Reutilizable (Pop-in) -->
    <Transition name="pop">
      <div
        v-if="confirmDialog.visible"
        class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm"
        role="alertdialog"
        aria-modal="true"
        @click.self="resolverConfirmacion(false)"
      >
        <div class="pop-card bg-white rounded-2xl shadow-2xl w-full max-w-sm overflow-hidden">
          <div :class="['h-1.5 bg-gradient-to-r', CONFIRM_VARIANTS[confirmDialog.variant].bar]"></div>
          <div class="px-6 pt-6 pb-4 text-center">
            <div :class="['w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4', CONFIRM_VARIANTS[confirmDialog.variant].iconWrap]">
              <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="CONFIRM_VARIANTS[confirmDialog.variant].icon" />
              </svg>
            </div>
            <h3 class="font-black text-slate-800 text-base">{{ confirmDialog.title }}</h3>
            <p class="text-xs text-slate-500 font-medium mt-1.5 leading-relaxed">{{ confirmDialog.message }}</p>

            <div
              v-if="confirmDialog.detalles.length"
              :class="['mt-4 border rounded-xl px-3 py-2.5 text-left space-y-1 max-h-40 overflow-y-auto', CONFIRM_VARIANTS[confirmDialog.variant].panel]"
            >
              <p v-for="(d, i) in confirmDialog.detalles" :key="i" class="text-[11px] font-semibold text-slate-600">{{ d }}</p>
            </div>
          </div>

          <div class="px-6 pb-6 pt-2 flex gap-2">
            <button
              ref="cancelBtnRef"
              type="button"
              @click="resolverConfirmacion(false)"
              class="flex-1 px-4 py-2.5 text-xs font-bold text-slate-500 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors focus:outline-none focus-visible:ring-4 focus-visible:ring-slate-300"
            >
              {{ confirmDialog.cancelText }}
            </button>
            <button
              ref="confirmBtnRef"
              type="button"
              @click="resolverConfirmacion(true)"
              :class="['flex-1 px-4 py-2.5 text-xs font-bold text-white rounded-xl shadow-md hover:shadow-lg hover:-translate-y-0.5 active:translate-y-0 transition-all focus:outline-none focus-visible:ring-4 focus-visible:ring-slate-300', CONFIRM_VARIANTS[confirmDialog.variant].button]"
            >
              {{ confirmDialog.confirmText }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Toasts Notificaciones Flotantes -->
    <div class="fixed top-5 right-5 z-[70] flex flex-col gap-2 w-80 max-w-[calc(100vw-2.5rem)]" aria-live="polite">
      <TransitionGroup name="toast">
        <div
          v-for="t in toasts"
          :key="t.id"
          role="alert"
          :class="['relative overflow-hidden flex items-start gap-3 pl-3 pr-3 pt-3 pb-4 rounded-xl shadow-xl border', TOAST_VARIANTS[t.type].wrap]"
        >
          <div :class="['w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0', TOAST_VARIANTS[t.type].iconWrap]">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="TOAST_VARIANTS[t.type].icon" />
            </svg>
          </div>
          <div class="flex-1 min-w-0 pt-0.5">
            <p class="text-xs font-black text-slate-800">{{ TOAST_VARIANTS[t.type].title }}</p>
            <p class="text-[11px] font-medium text-slate-500 leading-snug mt-0.5">{{ t.message }}</p>
          </div>
          <button @click="quitarToast(t.id)" class="opacity-40 hover:opacity-100 transition-opacity" aria-label="Cerrar notificación">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
          <div
            :class="['toast-progress absolute bottom-0 left-0 h-1', TOAST_VARIANTS[t.type].bar]"
            :style="{ animationDuration: t.duracion + 'ms' }"
          ></div>
        </div>
      </TransitionGroup>
    </div>
  </MainLayout>

  <!-- Modal: Ver Detalle Completo de Merma -->
<Transition name="fade">
  <div v-if="showDetalleModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
    <div class="modal-card bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden border border-slate-100 animate-scale-in">
      <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>
      
      <!-- Encabezado Modal -->
      <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
        <div>
          <h3 class="font-black text-slate-800 text-base">Registro de Merma #{{ mermaDetalle?.idMerma }}</h3>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Auditoría física y trazabilidad de corte</p>
        </div>
        <button @click="showDetalleModal = false" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition" aria-label="Cerrar">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Cuerpo del Detalle -->
      <div v-if="mermaDetalle" class="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
        <!-- Tarjeta de Origen y Prenda -->
        <div class="bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl px-4 py-3 text-white shadow-md shadow-purple-500/20">
          <div class="flex items-center justify-between">
            <span class="text-[10px] font-bold uppercase tracking-wider text-white/70">Orden de Producción</span>
            <span class="text-xs font-mono font-black text-white bg-white/20 px-2 py-0.5 rounded-md">
              #{{ mermaDetalle.idOrdenProduccion }}
            </span>
          </div>
          <p class="font-black text-sm mt-1">{{ mermaDetalle.nombrePrenda }}</p>
          <p class="text-[11px] text-white/80 font-medium">Pedido: {{ mermaDetalle.numeroPedido }}</p>
        </div>

        <!-- Cuadrícula de Datos Técnicos -->
        <div class="grid grid-cols-2 gap-3 bg-slate-50 border border-slate-200/80 rounded-2xl p-4 text-xs">
          <div>
            <span class="block text-[10px] font-bold text-slate-400 uppercase tracking-wider">Insumo / Tela</span>
            <span class="font-bold text-slate-800">{{ mermaDetalle.nombreInsumo }}</span>
            <span class="block text-[10px] font-mono text-slate-500">{{ mermaDetalle.codigoInsumo }}</span>
          </div>

          <div>
            <span class="block text-[10px] font-bold text-slate-400 uppercase tracking-wider">Cantidad Pesada</span>
            <span class="font-black font-mono text-sm text-slate-900">
              {{ Number(mermaDetalle.cantidadMermaReal).toFixed(2) }} {{ mermaDetalle.unidadMedida }}
            </span>
          </div>

          <div class="pt-2 border-t border-slate-200/60">
            <span class="block text-[10px] font-bold text-slate-400 uppercase tracking-wider">Clasificación</span>
            <span :class="['inline-flex items-center gap-1.5 px-2 py-0.5 mt-1 rounded-md text-[10px] font-bold border', TIPO_STYLES[mermaDetalle.tipoMerma]]">
              <span :class="['w-1.5 h-1.5 rounded-full', TIPO_DOT[mermaDetalle.tipoMerma]]"></span>
              {{ mermaDetalle.tipoMerma }}
            </span>
          </div>

          <div class="pt-2 border-t border-slate-200/60">
            <span class="block text-[10px] font-bold text-slate-400 uppercase tracking-wider">Bodega de Destino</span>
            <span v-if="mermaDetalle.nombreAlmacenDestino" class="font-bold text-emerald-700">
              {{ mermaDetalle.nombreAlmacenDestino }}
            </span>
            <span v-else class="text-slate-400 italic font-medium">Sin reingreso (Desecho)</span>
          </div>

          <div class="col-span-2 pt-2 border-t border-slate-200/60">
            <span class="block text-[10px] font-bold text-slate-400 uppercase tracking-wider">Fecha y Hora de Pesaje</span>
            <span class="font-bold text-slate-700">{{ formatFecha(mermaDetalle.fechaRegistro) }}</span>
          </div>
        </div>

        <!-- Bloque de Observaciones / Motivo de Auditoría -->
        <div>
          <span class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
            Observaciones / Motivo Declarado
          </span>
          <div class="p-3.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 leading-relaxed font-medium">
            <p v-if="mermaDetalle.observaciones" class="whitespace-pre-wrap">
              {{ mermaDetalle.observaciones }}
            </p>
            <p v-else class="text-slate-400 italic">
              No se registraron observaciones o justificaciones adicionales para esta merma.
            </p>
          </div>
        </div>
      </div>

      <!-- Footer Modal -->
      <div class="px-6 py-4 border-t border-slate-100 flex items-center justify-end bg-slate-50/50">
        <button
          @click="showDetalleModal = false"
          class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md hover:shadow-lg transition-all"
        >
          Cerrar
        </button>
      </div>
    </div>
  </div>
</Transition>
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

/* Modal Slide In */
.fade-enter-active .modal-card {
  animation: modal-in 0.25s ease-out;
}
@keyframes modal-in {
  from { transform: translateY(12px) scale(0.98); opacity: 0; }
  to { transform: translateY(0) scale(1); opacity: 1; }
}

/* Diálogo de Confirmación Pop-in */
.pop-enter-active,
.pop-leave-active {
  transition: opacity 0.2s ease;
}
.pop-enter-active .pop-card {
  animation: pop-in 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.pop-enter-from,
.pop-leave-to {
  opacity: 0;
}
@keyframes pop-in {
  from { transform: scale(0.85); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
}

/* Toasts */
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

@media (prefers-reduced-motion: reduce) {
  .fade-enter-active .modal-card,
  .pop-enter-active .pop-card {
    animation: none;
  }
  .toast-enter-active,
  .toast-leave-active {
    transition: opacity 0.2s ease;
  }
  .toast-enter-from,
  .toast-leave-to {
    transform: none;
  }
}
</style>