<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import { ordenProduccionService } from '../services/ordenProduccionService'
import { usePermissions } from '../composables/usePermissions'

const { can } = usePermissions()

const SECUENCIA_ESTADOS = ['Iniciada', 'En Corte', 'En Costura', 'Terminada']
const TODOS_LOS_ESTADOS = [...SECUENCIA_ESTADOS, 'Cancelada']

// Colores más saturados y con borde para que el estado se note de un vistazo en la tabla.
const ESTADO_STYLES = {
  'Iniciada': 'bg-slate-200 text-slate-700 border border-slate-300',
  'En Corte': 'bg-amber-100 text-amber-800 border border-amber-300',
  'En Costura': 'bg-indigo-100 text-indigo-800 border border-indigo-300',
  'Terminada': 'bg-emerald-100 text-emerald-800 border border-emerald-300',
  'Cancelada': 'bg-rose-100 text-rose-700 border border-rose-300'
}

const ESTADO_DOT = {
  'Iniciada': 'bg-slate-400',
  'En Corte': 'bg-amber-500',
  'En Costura': 'bg-indigo-500',
  'Terminada': 'bg-emerald-500',
  'Cancelada': 'bg-rose-500'
}

// Color de la barra de progreso según el estado de la orden
const ESTADO_BARRA = {
  'Iniciada': 'bg-gradient-to-r from-slate-400 to-slate-500',
  'En Corte': 'bg-gradient-to-r from-amber-400 to-orange-500',
  'En Costura': 'bg-gradient-to-r from-indigo-500 to-purple-600',
  'Terminada': 'bg-gradient-to-r from-emerald-400 to-emerald-600',
  'Cancelada': 'bg-rose-300'
}

// ---------- Diálogo de confirmación reutilizable ----------
// Variantes: danger (rojo), warning (ámbar), primary (morado), info (celeste)
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

    // Acciones destructivas: el foco inicia en "Volver" para evitar confirmar sin querer.
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

// ---------- Toasts (notificaciones) ----------
// Tipos: success, error, warning, info
const TOAST_VARIANTS = {
  success: {
    wrap: 'bg-white border-emerald-200 text-slate-700',
    iconWrap: 'bg-emerald-100 text-emerald-600',
    bar: 'bg-emerald-500',
    title: 'Listo',
    icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  error: {
    wrap: 'bg-white border-rose-200 text-slate-700',
    iconWrap: 'bg-rose-100 text-rose-600',
    bar: 'bg-rose-500',
    title: 'Algo salió mal',
    icon: 'M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  warning: {
    wrap: 'bg-white border-amber-200 text-slate-700',
    iconWrap: 'bg-amber-100 text-amber-600',
    bar: 'bg-amber-500',
    title: 'Atención',
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

// ---------- Estado base / listado paginado ----------
const ordenes = ref([])
const loading = ref(true)
const errorCarga = ref('')
const isSaving = ref(false)

const filtroEstado = ref('')
const busqueda = ref('')
const fechaDesde = ref('')
const fechaHasta = ref('')
const ordenAscendente = ref(false)
let busquedaTimeout = null

const paginacion = reactive({
  pagina: 1,
  tamanioPagina: 10,
  total: 0,
  totalPaginas: 1
})

const conteosPorEstado = ref({})

// ---------- Modal: Nueva orden (soporta lote: varias prendas a la vez) ----------
const showNuevaOrden = ref(false)
const pedidosDisponibles = ref([])
const cargandoPedidos = ref(false)
const formNueva = reactive({
  fechaInicio: new Date().toISOString().slice(0, 10)
})
const selectorIdPedidoDetalle = ref('')
const cantidadDraft = ref(null)
const itemsNueva = ref([]) // [{ idPedidoDetalle, idPedido, numeroPedido, nombreCliente, nombreArticulo, cantidadPendiente, cantidadProgramada }]
const erroresNueva = reactive({})

// Una vez agregada la primera prenda, el resto del lote debe ser del mismo pedido
// (lo exige también el backend): se filtra el selector para evitar errores.
const idPedidoLote = computed(() => itemsNueva.value[0]?.idPedido ?? null)

const opcionesSelector = computed(() =>
  pedidosDisponibles.value.filter(p =>
    !itemsNueva.value.some(it => it.idPedidoDetalle === p.idPedidoDetalle) &&
    (idPedidoLote.value === null || p.idPedido === idPedidoLote.value)
  )
)

const detalleSeleccionadoEnSelector = computed(() =>
  pedidosDisponibles.value.find(p => String(p.idPedidoDetalle) === String(selectorIdPedidoDetalle.value)) || null
)

const totalUnidadesLote = computed(() =>
  itemsNueva.value.reduce((s, it) => s + it.cantidadProgramada, 0)
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
    const resultado = await ordenProduccionService.obtenerOrdenes({
      estado: filtroEstado.value || undefined,
      busqueda: busqueda.value || undefined,
      fechaDesde: fechaDesde.value || undefined,
      fechaHasta: fechaHasta.value || undefined,
      pagina: paginacion.pagina,
      tamanioPagina: paginacion.tamanioPagina,
      ordenAscendente: ordenAscendente.value
    })
    ordenes.value = resultado.items
    paginacion.total = resultado.total
    paginacion.totalPaginas = Math.max(1, resultado.totalPaginas)
    conteosPorEstado.value = resultado.conteosPorEstado || {}
  } catch (e) {
    errorCarga.value = e?.response?.data?.message || 'No se pudieron cargar las órdenes de producción.'
  } finally {
    loading.value = false
  }
}

const recargarDesdeInicio = () => {
  paginacion.pagina = 1
  cargarOrdenes()
}

const onBusquedaInput = () => {
  clearTimeout(busquedaTimeout)
  busquedaTimeout = setTimeout(recargarDesdeInicio, 350)
}

const setFiltroEstado = (estado) => {
  filtroEstado.value = estado
  recargarDesdeInicio()
}

const onFechaFiltroChange = () => recargarDesdeInicio()

const alternarOrden = () => {
  ordenAscendente.value = !ordenAscendente.value
  recargarDesdeInicio()
}

const limpiarFiltrosFecha = () => {
  fechaDesde.value = ''
  fechaHasta.value = ''
  mostrarToast('info', 'Se quitó el filtro de fechas.', 2800)
  recargarDesdeInicio()
}

const irAPagina = (pagina) => {
  if (pagina < 1 || pagina > paginacion.totalPaginas || pagina === paginacion.pagina) return
  paginacion.pagina = pagina
  cargarOrdenes()
}

const paginasVisibles = computed(() => {
  const actual = paginacion.pagina
  const total = paginacion.totalPaginas
  const ventana = 2
  const inicio = Math.max(1, actual - ventana)
  const fin = Math.min(total, actual + ventana)
  const paginas = []
  for (let i = inicio; i <= fin; i++) paginas.push(i)
  return paginas
})

// Cierre con la tecla Escape (diálogo de confirmación primero, luego los modales)
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) {
    resolverConfirmacion(false)
  } else if (showAvance.value) {
    cerrarAvance()
  } else if (showNuevaOrden.value) {
    cerrarNuevaOrden()
  }
}

onMounted(() => {
  cargarOrdenes()
  window.addEventListener('keydown', onKeydown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeydown)
  clearTimeout(busquedaTimeout)
})

// ---------- KPIs (sobre el universo filtrado completo, no solo la página actual) ----------
const kpis = computed(() => ({
  total: paginacion.total,
  iniciadas: conteosPorEstado.value['Iniciada'] || 0,
  enProceso: (conteosPorEstado.value['En Corte'] || 0) + (conteosPorEstado.value['En Costura'] || 0),
  terminadas: conteosPorEstado.value['Terminada'] || 0,
  canceladas: conteosPorEstado.value['Cancelada'] || 0
}))

const FILTROS_ESTADO = [
  { v: '', l: 'Todas' },
  { v: 'Iniciada', l: 'Iniciada' },
  { v: 'En Corte', l: 'En Corte' },
  { v: 'En Costura', l: 'En Costura' },
  { v: 'Terminada', l: 'Terminada' },
  { v: 'Cancelada', l: 'Cancelada' }
]

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
const puedeCancelar = (orden) => orden.estadoOrden !== 'Terminada' && orden.estadoOrden !== 'Cancelada'
const puedeAvanzar = (orden) => orden.estadoOrden !== 'Terminada' && orden.estadoOrden !== 'Cancelada'

// ---------- Nueva orden / lote ----------
const abrirNuevaOrden = async () => {
  showNuevaOrden.value = true
  formNueva.fechaInicio = new Date().toISOString().slice(0, 10)
  selectorIdPedidoDetalle.value = ''
  cantidadDraft.value = null
  itemsNueva.value = []
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

const onSelectorChange = () => {
  cantidadDraft.value = detalleSeleccionadoEnSelector.value?.cantidadPendiente ?? null
}

const agregarItemAlLote = () => {
  erroresNueva.selector = ''
  const detalle = detalleSeleccionadoEnSelector.value
  const cantidad = Number(cantidadDraft.value)

  if (!detalle) {
    erroresNueva.selector = 'Selecciona una prenda para agregar.'
    return
  }
  if (!cantidad || cantidad <= 0) {
    erroresNueva.selector = 'La cantidad debe ser mayor a cero.'
    return
  }
  if (cantidad > detalle.cantidadPendiente) {
    erroresNueva.selector = `No puede superar el saldo pendiente (${detalle.cantidadPendiente}).`
    return
  }

  itemsNueva.value.push({
    idPedidoDetalle: detalle.idPedidoDetalle,
    idPedido: detalle.idPedido,
    numeroPedido: detalle.numeroPedido,
    nombreCliente: detalle.nombreCliente,
    nombreArticulo: detalle.nombreArticulo,
    nombreReceta: detalle.nombreReceta,
    cantidadPendiente: detalle.cantidadPendiente,
    cantidadProgramada: cantidad
  })

  selectorIdPedidoDetalle.value = ''
  cantidadDraft.value = null
}

const quitarItemDelLote = (idPedidoDetalle) => {
  itemsNueva.value = itemsNueva.value.filter(it => it.idPedidoDetalle !== idPedidoDetalle)
}

const validarNuevaOrden = () => {
  Object.keys(erroresNueva).forEach(k => delete erroresNueva[k])

  if (!formNueva.fechaInicio) {
    erroresNueva.fechaInicio = 'Indica la fecha de inicio.'
  }
  if (itemsNueva.value.length === 0) {
    erroresNueva.general = 'Agrega al menos una prenda a la orden.'
  }

  return Object.keys(erroresNueva).length === 0
}

const submitNuevaOrden = async () => {
  if (isSaving.value) return
  if (!validarNuevaOrden()) return

  const esLote = itemsNueva.value.length > 1

  const confirmado = await pedirConfirmacion({
    variant: 'primary',
    title: esLote ? '¿Crear lote de producción?' : '¿Crear orden de producción?',
    message: esLote
      ? `Se crearán ${itemsNueva.value.length} órdenes juntas (${totalUnidadesLote.value} unidades en total) con inicio el ${formatFecha(formNueva.fechaInicio)}.`
      : `Se creará la orden con inicio el ${formatFecha(formNueva.fechaInicio)}.`,
    detalles: itemsNueva.value.map(it => `${it.nombreArticulo} — ${it.cantidadProgramada} uds (${it.numeroPedido})`),
    confirmText: esLote ? 'Sí, crear lote' : 'Sí, crear orden',
    cancelText: 'Revisar'
  })
  if (!confirmado) return

  isSaving.value = true
  try {
    if (itemsNueva.value.length === 1) {
      const item = itemsNueva.value[0]
      await ordenProduccionService.crearOrden({
        idPedidoDetalle: item.idPedidoDetalle,
        fechaInicio: formNueva.fechaInicio,
        cantidadProgramada: item.cantidadProgramada
      })
    } else {
      await ordenProduccionService.crearLote({
        fechaInicio: formNueva.fechaInicio,
        items: itemsNueva.value.map(it => ({
          idPedidoDetalle: it.idPedidoDetalle,
          cantidadProgramada: it.cantidadProgramada
        }))
      })
    }
    showNuevaOrden.value = false
    mostrarToast('success', esLote ? 'Lote creado correctamente.' : 'Orden creada correctamente.')
    await cargarOrdenes()
  } catch (e) {
    const msg = e?.response?.data?.message || 'Ocurrió un error al crear la orden.'
    erroresNueva.general = msg
    mostrarToast('error', msg)
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

const porcentajeAvance = computed(() => {
  if (!ordenSeleccionada.value?.cantidadProgramada) return 0
  return Math.min(100, Math.round((Number(formAvance.cantidadProducida || 0) / ordenSeleccionada.value.cantidadProgramada) * 100))
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

  const orden = ordenSeleccionada.value
  const cambiaEtapa = formAvance.estadoOrden !== etapaOriginal.value

  if (formAvance.estadoOrden === 'Terminada') {
    // Irreversible: la orden ya no admite cambios y puede finalizar el pedido.
    const confirmado = await pedirConfirmacion({
      variant: 'warning',
      title: '¿Marcar la orden como Terminada?',
      message: `Se completarán las ${orden.cantidadProgramada} unidades programadas y la orden no admitirá más cambios.`,
      detalles: [`Orden #${orden.idOrden} — ${orden.nombreArticulo}`, `Pedido ${orden.numeroPedido} · ${orden.nombreCliente}`],
      confirmText: 'Sí, terminar',
      cancelText: 'Volver'
    })
    if (!confirmado) return
  } else if (cambiaEtapa) {
    // Cambio de etapa informativo: el contador se reinicia para la nueva etapa.
    const confirmado = await pedirConfirmacion({
      variant: 'info',
      title: `¿Pasar a "${formAvance.estadoOrden}"?`,
      message: `La orden saldrá de "${etapaOriginal.value}". El contador de la nueva etapa empieza en 0 y no podrás retroceder.`,
      detalles: [`Orden #${orden.idOrden} — ${orden.nombreArticulo}`, `${etapaOriginal.value} → ${formAvance.estadoOrden}`],
      confirmText: 'Sí, avanzar',
      cancelText: 'Volver'
    })
    if (!confirmado) return
  }

  isSaving.value = true
  try {
    await ordenProduccionService.registrarAvance(orden.idOrden, {
      estadoOrden: formAvance.estadoOrden,
      cantidadProducida: Number(formAvance.cantidadProducida)
    })
    const estadoGuardado = formAvance.estadoOrden
    showAvance.value = false
    ordenSeleccionada.value = null
    mostrarToast(
      'success',
      estadoGuardado === 'Terminada'
        ? `Orden #${orden.idOrden} terminada.`
        : cambiaEtapa
          ? `Orden #${orden.idOrden} avanzó a ${estadoGuardado}.`
          : 'Avance registrado correctamente.'
    )
    await cargarOrdenes()
  } catch (e) {
    const msg = e?.response?.data?.message || 'Ocurrió un error al registrar el avance.'
    erroresAvance.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

// ---------- Cancelar / Eliminar ----------
const cancelarOrden = async (orden) => {
  if (isSaving.value) return

  const confirmado = await pedirConfirmacion({
    variant: 'danger',
    title: '¿Cancelar esta orden?',
    message: 'Quedará registrada como Cancelada y su cantidad volverá a estar disponible para programarse.',
    detalles: [`Orden #${orden.idOrden} — ${orden.nombreArticulo}`, `Pedido ${orden.numeroPedido} · ${orden.nombreCliente}`],
    confirmText: 'Sí, cancelar orden',
    cancelText: 'No, volver'
  })
  if (!confirmado) return

  isSaving.value = true
  try {
    await ordenProduccionService.cancelarOrden(orden.idOrden)
    mostrarToast('success', `Orden #${orden.idOrden} cancelada.`)
    await cargarOrdenes()
  } catch (e) {
    const msg = e?.response?.data?.message || 'No se pudo cancelar la orden.'
    errorCarga.value = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

const eliminarOrden = async (orden) => {
  if (isSaving.value) return

  const confirmado = await pedirConfirmacion({
    variant: 'danger',
    title: '¿Eliminar definitivamente?',
    message: 'Esta acción no se puede deshacer.',
    detalles: [`Orden #${orden.idOrden} — ${orden.nombreArticulo}`, `Pedido ${orden.numeroPedido} · ${orden.nombreCliente}`],
    confirmText: 'Sí, eliminar',
    cancelText: 'No, volver'
  })
  if (!confirmado) return

  isSaving.value = true
  try {
    await ordenProduccionService.eliminarOrden(orden.idOrden)
    mostrarToast('success', `Orden #${orden.idOrden} eliminada.`)
    await cargarOrdenes()
  } catch (e) {
    const msg = e?.response?.data?.message || 'No se pudo eliminar la orden.'
    errorCarga.value = msg
    mostrarToast('error', msg)
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
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-lg font-black text-slate-800">Órdenes de Producción</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de corte, costura y avance de producción por pedido</p>
        </div>
        <button
          v-if="can('PRODUCCION', 'insertar')"
          @click="abrirNuevaOrden"
          class="inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:-translate-y-0.5 active:translate-y-0 transition-all focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/30"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Nueva Orden
        </button>
      </div>

      <!-- KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-5 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Total</p>
            <p class="text-2xl font-black text-slate-800 mt-1">{{ kpis.total }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-slate-100 text-slate-500 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" /></svg>
          </div>
        </div>
        <div class="bg-white rounded-2xl shadow-md border-2 border-slate-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Iniciadas</p>
            <p class="text-2xl font-black text-slate-700 mt-1">{{ kpis.iniciadas }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-slate-100 text-slate-500 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 3l14 9-14 9V3z" /></svg>
          </div>
        </div>
        <div class="bg-white rounded-2xl shadow-md border-2 border-indigo-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-indigo-500 uppercase tracking-wide">En Proceso</p>
            <p class="text-2xl font-black text-indigo-700 mt-1">{{ kpis.enProceso }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-indigo-100 text-indigo-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" /></svg>
          </div>
        </div>
        <div class="bg-white rounded-2xl shadow-md border-2 border-emerald-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-emerald-600 uppercase tracking-wide">Terminadas</p>
            <p class="text-2xl font-black text-emerald-700 mt-1">{{ kpis.terminadas }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-emerald-100 text-emerald-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
          </div>
        </div>
        <div class="bg-white rounded-2xl shadow-md border-2 border-rose-200 p-4 flex items-center justify-between gap-3 col-span-2 lg:col-span-1">
          <div>
            <p class="text-[10px] font-bold text-rose-500 uppercase tracking-wide">Canceladas</p>
            <p class="text-2xl font-black text-rose-700 mt-1">{{ kpis.canceladas }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-rose-100 text-rose-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </div>
        </div>
      </div>

      <!-- Filtros -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 space-y-3">
        <div class="flex flex-col md:flex-row md:items-center gap-3 md:gap-4">
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
              v-for="opcion in FILTROS_ESTADO"
              :key="opcion.v"
              @click="setFiltroEstado(opcion.v)"
              :class="[
                'px-3 py-1.5 rounded-lg text-[11px] font-bold transition-all',
                filtroEstado === opcion.v
                  ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm shadow-purple-500/30'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              ]"
            >
              {{ opcion.l }}
            </button>
          </div>
        </div>

        <div class="flex flex-wrap items-end gap-3 pt-3 border-t border-slate-100">
          <div>
            <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Desde</label>
            <input
              v-model="fechaDesde"
              @change="onFechaFiltroChange"
              type="date"
              class="mt-1 block px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
            />
          </div>
          <div>
            <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Hasta</label>
            <input
              v-model="fechaHasta"
              @change="onFechaFiltroChange"
              type="date"
              class="mt-1 block px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
            />
          </div>
          <button
            v-if="fechaDesde || fechaHasta"
            @click="limpiarFiltrosFecha"
            class="px-2.5 py-1.5 text-[11px] font-bold text-slate-400 hover:text-slate-600"
          >
            Limpiar fechas
          </button>
          <button
            @click="alternarOrden"
            class="ml-auto inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[11px] font-bold bg-slate-100 text-slate-600 hover:bg-slate-200 transition-colors"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path v-if="ordenAscendente" stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 4h13M3 8h9M3 12h6m6 7l3-3m0 0l3 3m-3-3v12" />
              <path v-else stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 4h13M3 8h9M3 12h6m9 1V6m0 12l-3-3m3 3l3-3" />
            </svg>
            Fecha {{ ordenAscendente ? 'más antigua primero' : 'más reciente primero' }}
          </button>
        </div>
      </div>

      <!-- Mensaje de error general -->
      <div v-if="errorCarga" class="flex items-start gap-3 bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold rounded-xl px-4 py-3" role="alert">
        <svg class="w-4 h-4 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        <p class="flex-1">{{ errorCarga }}</p>
        <button @click="errorCarga = ''" class="opacity-50 hover:opacity-100" aria-label="Cerrar mensaje">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
        </button>
      </div>

      <!-- Tabla -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <!-- Cargando: filas esqueleto -->
        <div v-if="loading" class="divide-y divide-slate-50" aria-busy="true">
          <div v-for="n in 5" :key="n" class="px-5 py-4 flex items-center gap-4 animate-pulse">
            <div class="flex-1 space-y-2">
              <div class="h-3 bg-slate-100 rounded w-24"></div>
              <div class="h-2.5 bg-slate-100 rounded w-36"></div>
            </div>
            <div class="flex-1 space-y-2 hidden sm:block">
              <div class="h-3 bg-slate-100 rounded w-32"></div>
              <div class="h-2.5 bg-slate-100 rounded w-24"></div>
            </div>
            <div class="h-2 bg-slate-100 rounded-full w-32"></div>
            <div class="h-6 bg-slate-100 rounded-lg w-20"></div>
          </div>
        </div>

        <!-- Vacío -->
        <div v-else-if="ordenes.length === 0" class="text-center py-16 px-4">
          <div class="w-14 h-14 rounded-2xl bg-purple-50 text-purple-400 flex items-center justify-center mx-auto mb-3">
            <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" /></svg>
          </div>
          <p class="text-sm font-bold text-slate-600">No hay órdenes de producción con estos filtros</p>
          <p class="text-xs text-slate-400 mt-1">Crea una nueva orden a partir de un pedido con saldo pendiente.</p>
        </div>

        <div v-else class="overflow-x-auto">
          <table class="w-full text-xs">
            <thead>
              <tr class="border-b-2 border-slate-100 text-left bg-slate-50/60">
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Pedido / Cliente</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Artículo / Receta</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] cursor-pointer select-none hover:text-purple-600 transition-colors" @click="alternarOrden">
                  Inicio
                  <svg class="w-3 h-3 inline-block ml-0.5" :class="ordenAscendente ? 'rotate-180' : ''" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 3a1 1 0 01.707.293l4 4a1 1 0 01-1.414 1.414L10 5.414 6.707 8.707a1 1 0 01-1.414-1.414l4-4A1 1 0 0110 3zm0 14a1 1 0 01-.707-.293l-4-4a1 1 0 111.414-1.414L10 14.586l3.293-3.293a1 1 0 111.414 1.414l-4 4A1 1 0 0110 17z" clip-rule="evenodd"/></svg>
                </th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Progreso</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Estado</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="orden in ordenes"
                :key="orden.idOrden"
                class="border-b border-slate-50 last:border-0 hover:bg-purple-50/30 transition-colors"
              >
                <td class="px-5 py-3.5">
                  <p class="font-bold text-slate-700">{{ orden.numeroPedido }}</p>
                  <p class="text-slate-400">{{ orden.nombreCliente }}</p>
                  <span v-if="orden.idLote" class="inline-block mt-1 px-1.5 py-0.5 rounded bg-purple-50 text-purple-600 text-[9px] font-bold">
                    Lote #{{ orden.idLote }}
                  </span>
                </td>
                <td class="px-5 py-3.5">
                  <p class="font-semibold text-slate-700">{{ orden.nombreArticulo }}</p>
                  <p class="text-slate-400">{{ orden.nombreReceta }}</p>
                </td>
                <td class="px-5 py-3.5 text-slate-600 font-medium">{{ formatFecha(orden.fechaInicio) }}</td>
                <td class="px-5 py-3.5 min-w-[160px]">
                  <div class="flex items-center gap-2">
                    <div class="flex-1 h-2 bg-slate-100 rounded-full overflow-hidden">
                      <div
                        class="h-full rounded-full transition-all duration-500"
                        :class="ESTADO_BARRA[orden.estadoOrden] || 'bg-purple-500'"
                        :style="{ width: porcentaje(orden) + '%' }"
                      ></div>
                    </div>
                    <span class="text-slate-600 font-bold whitespace-nowrap">{{ orden.cantidadProducida }}/{{ orden.cantidadProgramada }}</span>
                  </div>
                  <p class="text-[10px] text-slate-400 font-semibold mt-0.5">{{ porcentaje(orden) }}% completado</p>
                </td>
                <td class="px-5 py-3.5">
                  <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[10px] font-bold', ESTADO_STYLES[orden.estadoOrden]]">
                    <span :class="['w-1.5 h-1.5 rounded-full', ESTADO_DOT[orden.estadoOrden]]"></span>
                    {{ orden.estadoOrden }}
                  </span>
                </td>
                <td class="px-5 py-3.5">
                  <div class="flex items-center justify-end gap-1.5">
                    <button
                      v-if="can('PRODUCCION', 'modificar') && puedeAvanzar(orden)"
                      @click="abrirAvance(orden)"
                      :disabled="isSaving"
                      class="px-2.5 py-1.5 rounded-lg bg-purple-50 text-purple-700 font-bold text-[10px] hover:bg-purple-100 hover:-translate-y-0.5 transition-all disabled:opacity-50"
                    >
                      Avanzar
                    </button>
                    <button
                      v-if="can('PRODUCCION', 'eliminar') && puedeEliminar(orden)"
                      @click="eliminarOrden(orden)"
                      :disabled="isSaving"
                      class="px-2.5 py-1.5 rounded-lg bg-rose-50 text-rose-600 font-bold text-[10px] hover:bg-rose-100 hover:-translate-y-0.5 transition-all disabled:opacity-50"
                    >
                      Eliminar
                    </button>
                    <button
                      v-else-if="can('PRODUCCION', 'eliminar') && puedeCancelar(orden)"
                      @click="cancelarOrden(orden)"
                      :disabled="isSaving"
                      class="px-2.5 py-1.5 rounded-lg bg-slate-100 text-slate-500 font-bold text-[10px] hover:bg-rose-50 hover:text-rose-600 hover:-translate-y-0.5 transition-all disabled:opacity-50"
                    >
                      Cancelar
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Paginado -->
        <div v-if="!loading && ordenes.length > 0" class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 px-5 py-4 border-t border-slate-100">
          <p class="text-[11px] text-slate-500 font-semibold">
            Mostrando {{ (paginacion.pagina - 1) * paginacion.tamanioPagina + 1 }}–{{ Math.min(paginacion.pagina * paginacion.tamanioPagina, paginacion.total) }}
            de {{ paginacion.total }} órdenes
          </p>
          <div class="flex items-center gap-1">
            <button
              @click="irAPagina(paginacion.pagina - 1)"
              :disabled="paginacion.pagina === 1"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-slate-500 font-bold text-xs hover:bg-slate-100 disabled:opacity-30 disabled:hover:bg-transparent transition-colors"
            >
              ‹
            </button>
            <button
              v-if="paginasVisibles[0] > 1"
              @click="irAPagina(1)"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-[11px] font-bold text-slate-500 hover:bg-slate-100 transition-colors"
            >
              1
            </button>
            <span v-if="paginasVisibles[0] > 2" class="text-slate-300 text-[11px]">…</span>
            <button
              v-for="p in paginasVisibles"
              :key="p"
              @click="irAPagina(p)"
              :class="[
                'w-7 h-7 flex items-center justify-center rounded-lg text-[11px] font-bold transition-colors',
                p === paginacion.pagina
                  ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm'
                  : 'text-slate-500 hover:bg-slate-100'
              ]"
            >
              {{ p }}
            </button>
            <span v-if="paginasVisibles[paginasVisibles.length - 1] < paginacion.totalPaginas - 1" class="text-slate-300 text-[11px]">…</span>
            <button
              v-if="paginasVisibles[paginasVisibles.length - 1] < paginacion.totalPaginas"
              @click="irAPagina(paginacion.totalPaginas)"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-[11px] font-bold text-slate-500 hover:bg-slate-100 transition-colors"
            >
              {{ paginacion.totalPaginas }}
            </button>
            <button
              @click="irAPagina(paginacion.pagina + 1)"
              :disabled="paginacion.pagina === paginacion.totalPaginas"
              class="w-7 h-7 flex items-center justify-center rounded-lg text-slate-500 font-bold text-xs hover:bg-slate-100 disabled:opacity-30 disabled:hover:bg-transparent transition-colors"
            >
              ›
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal: Nueva Orden (soporta lote de varias prendas) -->
    <Transition name="fade">
      <div v-if="showNuevaOrden" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-2xl shadow-2xl w-full max-w-xl overflow-hidden">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h3 class="font-black text-slate-800 text-sm">Nueva Orden de Producción</h3>
              <p class="text-[11px] text-slate-400 font-medium mt-0.5">Puedes agregar varias prendas del mismo pedido para trabajarlas juntas (mismo lote)</p>
            </div>
            <button @click="cerrarNuevaOrden" class="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100" aria-label="Cerrar">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="px-6 py-5 space-y-4 max-h-[70vh] overflow-y-auto">
            <div v-if="erroresNueva.general" class="flex items-start gap-2 bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold rounded-xl px-3 py-2" role="alert">
              <svg class="w-4 h-4 flex-shrink-0 mt-px" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              {{ erroresNueva.general }}
            </div>

            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Fecha de inicio (aplica a todas las prendas del lote)</label>
              <input
                v-model="formNueva.fechaInicio"
                type="date"
                class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
              />
              <p v-if="erroresNueva.fechaInicio" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresNueva.fechaInicio }}</p>
            </div>

            <!-- Prendas ya agregadas a este lote -->
            <div v-if="itemsNueva.length" class="space-y-1.5">
              <div class="flex items-center justify-between">
                <p class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Prendas en esta orden</p>
                <span class="px-2 py-0.5 rounded-full bg-purple-100 text-purple-700 text-[10px] font-bold">
                  {{ itemsNueva.length }} {{ itemsNueva.length === 1 ? 'prenda' : 'prendas' }} · {{ totalUnidadesLote }} uds
                </span>
              </div>
              <div class="border border-slate-100 rounded-xl divide-y divide-slate-50 overflow-hidden">
                <div v-for="item in itemsNueva" :key="item.idPedidoDetalle" class="px-3 py-2.5 flex items-center justify-between gap-2 bg-purple-50/40">
                  <div class="text-[11px]">
                    <p class="font-bold text-slate-700">{{ item.nombreArticulo }}</p>
                    <p class="text-slate-400">{{ item.numeroPedido }} — {{ item.nombreCliente }} · {{ item.cantidadProgramada }} uds</p>
                  </div>
                  <button @click="quitarItemDelLote(item.idPedidoDetalle)" class="text-rose-400 hover:text-rose-600 p-1 rounded-lg hover:bg-rose-50" aria-label="Quitar prenda">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  </button>
                </div>
              </div>
            </div>

            <!-- Selector para agregar una prenda más -->
            <div class="border border-dashed border-slate-200 rounded-xl p-3 space-y-3">
              <div>
                <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">
                  {{ itemsNueva.length ? 'Agregar otra prenda de este pedido' : 'Pedido pendiente de programar' }}
                </label>
                <select
                  v-model="selectorIdPedidoDetalle"
                  @change="onSelectorChange"
                  class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                >
                  <option value="" disabled>{{ cargandoPedidos ? 'Cargando pedidos...' : 'Selecciona una prenda' }}</option>
                  <option v-for="p in opcionesSelector" :key="p.idPedidoDetalle" :value="p.idPedidoDetalle">
                    {{ p.numeroPedido }} — {{ p.nombreCliente }} — {{ p.nombreArticulo }} (pend. {{ p.cantidadPendiente }})
                  </option>
                </select>
                <p v-if="!cargandoPedidos && opcionesSelector.length === 0 && itemsNueva.length === 0" class="text-slate-400 text-[11px] mt-1">
                  No hay pedidos con saldo pendiente de programar.
                </p>
                <p v-if="!cargandoPedidos && opcionesSelector.length === 0 && itemsNueva.length > 0" class="text-slate-400 text-[11px] mt-1">
                  No quedan más prendas pendientes de este pedido.
                </p>
              </div>

              <div v-if="detalleSeleccionadoEnSelector" class="bg-purple-50/60 border border-purple-100 rounded-xl px-4 py-3 text-[11px] text-slate-600 space-y-0.5">
                <p><span class="font-bold text-slate-700">Receta:</span> {{ detalleSeleccionadoEnSelector.nombreReceta }}</p>
                <p><span class="font-bold text-slate-700">Cantidad pedida:</span> {{ detalleSeleccionadoEnSelector.cantidadPedida }}</p>
                <p><span class="font-bold text-emerald-700">Saldo disponible:</span> {{ detalleSeleccionadoEnSelector.cantidadPendiente }}</p>

                <div v-if="detalleSeleccionadoEnSelector.ordenesExistentes?.length" class="pt-1.5">
                  <p class="font-bold text-slate-700 mb-1">Lotes ya programados:</p>
                  <div class="flex flex-wrap gap-1.5">
                    <span
                      v-for="lote in detalleSeleccionadoEnSelector.ordenesExistentes"
                      :key="lote.idOrden"
                      :class="['px-2 py-0.5 rounded text-[9px] font-bold', ESTADO_STYLES[lote.estadoOrden]]"
                    >
                      #{{ lote.idOrden }} · {{ lote.cantidadProgramada }} uds · {{ lote.estadoOrden }}
                    </span>
                  </div>
                </div>
              </div>

              <div v-if="detalleSeleccionadoEnSelector" class="flex items-end gap-2">
                <div class="flex-1">
                  <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Cantidad a programar</label>
                  <input
                    v-model="cantidadDraft"
                    type="number"
                    min="1"
                    :max="detalleSeleccionadoEnSelector.cantidadPendiente"
                    class="mt-1 w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs text-slate-700 focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                  />
                </div>
                <button
                  type="button"
                  @click="agregarItemAlLote"
                  class="px-4 py-2 text-xs font-bold text-white bg-slate-700 rounded-xl hover:bg-slate-800 transition-colors"
                >
                  Agregar
                </button>
              </div>
              <p v-if="erroresNueva.selector" class="text-rose-500 text-[11px] font-semibold">{{ erroresNueva.selector }}</p>
            </div>

            <!-- Aviso informativo -->
            <div class="flex items-start gap-2 bg-sky-50 border border-sky-100 text-sky-700 text-[11px] font-medium rounded-xl px-3 py-2.5">
              <svg class="w-4 h-4 flex-shrink-0 mt-px" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              <p>Puedes programar solo una parte del saldo disponible de cada prenda ahora y crear otra orden más tarde para el resto.</p>
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
              :disabled="isSaving || itemsNueva.length === 0"
              class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 disabled:opacity-60 transition-all"
            >
              {{ isSaving ? 'Guardando...' : itemsNueva.length > 1 ? `Crear Lote (${itemsNueva.length} prendas)` : 'Crear Orden' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Modal: Registrar Avance -->
    <Transition name="fade">
      <div v-if="showAvance" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h3 class="font-black text-slate-800 text-sm">Registrar Avance</h3>
              <p v-if="ordenSeleccionada" class="text-[11px] text-slate-400 font-medium mt-0.5">
                Orden #{{ ordenSeleccionada.idOrden }} — {{ ordenSeleccionada.numeroPedido }}
                <span v-if="ordenSeleccionada.idLote" class="text-purple-500">· Lote #{{ ordenSeleccionada.idLote }}</span>
              </p>
            </div>
            <button @click="cerrarAvance" class="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100" aria-label="Cerrar">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div v-if="ordenSeleccionada" class="px-6 py-5 space-y-4 max-h-[75vh] overflow-y-auto">
            <div v-if="erroresAvance.general" class="flex items-start gap-2 bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold rounded-xl px-3 py-2" role="alert">
              <svg class="w-4 h-4 flex-shrink-0 mt-px" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              {{ erroresAvance.general }}
            </div>

            <!-- Prenda que se está avanzando: resaltada para que no haya duda de "cuál es cuál" -->
            <div class="bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl px-4 py-3 text-white shadow-md shadow-purple-500/20">
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
            <p v-else-if="cargandoContexto" class="text-[11px] text-slate-400 animate-pulse">Cargando contexto del pedido...</p>

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

              <div class="mt-2 flex items-center gap-2">
                <div class="flex-1 h-2 bg-slate-100 rounded-full overflow-hidden">
                  <div
                    class="h-full bg-gradient-to-r from-purple-600 to-pink-500 rounded-full transition-all duration-300"
                    :style="{ width: porcentajeAvance + '%' }"
                  ></div>
                </div>
                <span class="text-[11px] font-bold text-slate-500 w-9 text-right">{{ porcentajeAvance }}%</span>
              </div>
            </div>

            <!-- Aviso informativo: nueva etapa -->
            <div
              v-if="formAvance.estadoOrden !== etapaOriginal && formAvance.estadoOrden !== 'Terminada'"
              class="flex items-start gap-2 bg-sky-50 border border-sky-100 text-sky-700 text-[11px] font-medium rounded-xl px-3 py-2.5"
            >
              <svg class="w-4 h-4 flex-shrink-0 mt-px" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              <p>Estás entrando a una etapa nueva: el contador se reinició a 0, independiente de lo registrado en "{{ etapaOriginal }}".</p>
            </div>

            <!-- Aviso de advertencia: terminar la orden -->
            <div
              v-if="formAvance.estadoOrden === 'Terminada'"
              class="flex items-start gap-2 bg-emerald-50 border border-emerald-100 text-emerald-700 text-[11px] font-semibold rounded-xl px-3 py-2.5"
            >
              <svg class="w-4 h-4 flex-shrink-0 mt-px" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              <p>
                Al guardar, esta orden quedará marcada como Terminada (se completan las {{ ordenSeleccionada.cantidadProgramada }} unidades programadas) y no admitirá más cambios.
                Si esto completa todas las prendas del pedido, el pedido pasará automáticamente a "Finalizado".
              </p>
            </div>
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

    <!-- Diálogo de confirmación (reutilizable: danger, warning, primary, info) -->
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

    <!-- Toasts (success, error, warning, info) -->
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

/* Los modales grandes entran con un leve deslizamiento */
.fade-enter-active .modal-card {
  animation: modal-in 0.25s ease-out;
}
@keyframes modal-in {
  from { transform: translateY(12px) scale(0.98); opacity: 0; }
  to { transform: translateY(0) scale(1); opacity: 1; }
}

/* Diálogo de confirmación */
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