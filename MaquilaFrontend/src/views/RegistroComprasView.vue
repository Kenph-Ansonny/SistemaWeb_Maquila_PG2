<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import compraService from '../services/compraService'
import proveedorService from '../services/proveedorService'
import articuloService from '../services/articuloService'
import almacenService from '../services/almacenService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

const ESTADOS = ['Pendiente', 'Recibido', 'Anulado']

const ESTADO_STYLES = {
  'Pendiente': 'bg-amber-100 text-amber-800 border-amber-300',
  'Recibido': 'bg-emerald-100 text-emerald-800 border-emerald-300',
  'Anulado': 'bg-rose-100 text-rose-800 border-rose-300'
}

const ESTADO_DOT = {
  'Pendiente': 'bg-amber-500',
  'Recibido': 'bg-emerald-500',
  'Anulado': 'bg-rose-500'
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

// ---------- Estado Principal ----------
const compras = ref([])
const proveedores = ref([])
const articulos = ref([])
const almacenes = ref([])
const isLoading = ref(true)
const isSaving = ref(false)

// Filtros
const filtroBusqueda = ref('')
const filtroEstado = ref('')
const filtroProveedor = ref('')
const fechaDesde = ref('')
const fechaHasta = ref('')
let busquedaTimeout = null

// Modales
const showModal = ref(false)
const showDetalleModal = ref(false)
const compraDetalle = ref(null)

// Formulario de Nueva Compra
const formCompra = reactive({
  numeroDocumento: '',
  idProveedor: '',
  fechaCompra: new Date().toISOString().slice(0, 10),
  detalles: []
})

// Item auxiliar para la barra de captura de renglón
const itemForm = reactive({
  idArticulo: '',
  idAlmacenDestino: '',
  cantidad: null,
  costoUnitario: null
})

const editandoIndex = ref(null)
const erroresForm = reactive({})

const formatMoneda = (val) =>
  new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(val || 0)

const formatFecha = (valor) => {
  if (!valor) return '—'
  return new Date(valor).toLocaleDateString('es-GT', {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  })
}

// Materias primas e insumos permitidos para compra
const articulosComprables = computed(() =>
  articulos.value.filter(a => a.tipoArticulo === 'MateriaPrima' || a.tipoArticulo === 'Insumo')
)

const articuloSeleccionadoInfo = computed(() =>
  articulos.value.find(a => a.idArticulo === Number(itemForm.idArticulo)) || null
)

const totalCompraForm = computed(() =>
  formCompra.detalles.reduce((acc, d) => acc + (d.cantidad * d.costoUnitario), 0)
)

// ---------- KPIs ----------
const kpis = computed(() => {
  const lista = compras.value
  return {
    total: lista.length,
    pendientes: lista.filter(c => c.estadoCompra === 'Pendiente').length,
    recibidas: lista.filter(c => c.estadoCompra === 'Recibido').length,
    montoTotal: lista.filter(c => c.estadoCompra !== 'Anulado').reduce((acc, c) => acc + Number(c.costoTotal || 0), 0)
  }
})

// ---------- Carga de Datos ----------
const cargarCompras = async () => {
  if (!can('COMPRAS', 'consultar')) {
    isLoading.value = false
    return
  }
  isLoading.value = true
  try {
    const data = await compraService.obtenerTodas({
      estado: filtroEstado.value || undefined,
      idProveedor: filtroProveedor.value || undefined,
      fechaDesde: fechaDesde.value || undefined,
      fechaHasta: fechaHasta.value || undefined,
      busqueda: filtroBusqueda.value || undefined
    })
    compras.value = Array.isArray(data) ? data : (data?.data || [])
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'Error al obtener el historial de compras.')
  } finally {
    isLoading.value = false
  }
}

const cargarCatalogos = async () => {
  try {
    const [resProv, resArt, resAlm] = await Promise.all([
      proveedorService.obtenerOpciones(),
      articuloService.obtenerTodos(),
      almacenService.obtenerTodos()
    ])
    proveedores.value = Array.isArray(resProv) ? resProv : (resProv?.data || [])
    articulos.value = Array.isArray(resArt) ? resArt : (resArt?.data || [])
    almacenes.value = (Array.isArray(resAlm) ? resAlm : (resAlm?.data || [])).filter(a => a.estadoAlmacen)
  } catch {
    mostrarToast('error', 'Error al sincronizar catálogos auxiliares.')
  }
}

const onBusquedaInput = () => {
  clearTimeout(busquedaTimeout)
  busquedaTimeout = setTimeout(cargarCompras, 350)
}

const onArticuloChange = () => {
  const art = articuloSeleccionadoInfo.value
  if (art) {
    itemForm.costoUnitario = art.costoPromedio > 0 ? Number(art.costoPromedio.toFixed(4)) : null
  }
}

// ---------- Gestión de Renglones ----------
const procesarRenglon = () => {
  erroresForm.renglon = ''
  const { idArticulo, idAlmacenDestino, cantidad, costoUnitario } = itemForm

  if (!idArticulo) {
    erroresForm.renglon = 'Selecciona el artículo o insumo a comprar.'
    return
  }
  if (!idAlmacenDestino) {
    erroresForm.renglon = 'Selecciona la bodega destino de recepción.'
  }
  if (!cantidad || cantidad <= 0) {
    erroresForm.renglon = 'La cantidad debe ser mayor a cero.'
    return
  }
  if (costoUnitario === null || costoUnitario === undefined || costoUnitario < 0) {
    erroresForm.renglon = 'El costo unitario no puede ser negativo.'
    return
  }

  const art = articulos.value.find(a => a.idArticulo === Number(idArticulo))
  const alm = almacenes.value.find(a => a.idAlmacen === Number(idAlmacenDestino))

  if (editandoIndex.value !== null) {
    formCompra.detalles[editandoIndex.value] = {
      idArticulo: Number(idArticulo),
      codigoArticulo: art?.codigoArticulo || '',
      nombreArticulo: art?.nombreArticulo || '',
      idUnidadMedida: art?.idUnidadBaseMedida,
      codigoMedida: art?.codigoMedida || 'Uds',
      idAlmacenDestino: Number(idAlmacenDestino),
      nombreAlmacenDestino: alm?.nombreAlmacen || '',
      cantidad: Number(cantidad),
      costoUnitario: Number(costoUnitario),
      subtotal: Math.round(Number(cantidad) * Number(costoUnitario) * 100) / 100
    }
    editandoIndex.value = null
    mostrarToast('success', 'Renglón actualizado en la tabla.')
  } else {
    // Si ya existe en la misma bodega, sumar o advertir
    const existe = formCompra.detalles.some(d => d.idArticulo === Number(idArticulo) && d.idAlmacenDestino === Number(idAlmacenDestino))
    if (existe) {
      erroresForm.renglon = 'Este insumo ya fue agregado para la misma bodega. Edita su renglón.'
      return
    }

    formCompra.detalles.push({
      idArticulo: Number(idArticulo),
      codigoArticulo: art?.codigoArticulo || '',
      nombreArticulo: art?.nombreArticulo || '',
      idUnidadMedida: art?.idUnidadBaseMedida,
      codigoMedida: art?.codigoMedida || 'Uds',
      idAlmacenDestino: Number(idAlmacenDestino),
      nombreAlmacenDestino: alm?.nombreAlmacen || '',
      cantidad: Number(cantidad),
      costoUnitario: Number(costoUnitario),
      subtotal: Math.round(Number(cantidad) * Number(costoUnitario) * 100) / 100
    })
  }

  itemForm.idArticulo = ''
  itemForm.cantidad = null
  itemForm.costoUnitario = null
}

const seleccionarParaEditarRenglon = (index) => {
  editandoIndex.value = index
  const linea = formCompra.detalles[index]
  itemForm.idArticulo = linea.idArticulo
  itemForm.idAlmacenDestino = linea.idAlmacenDestino
  itemForm.cantidad = linea.cantidad
  itemForm.costoUnitario = linea.costoUnitario
}

const quitarRenglon = (index) => {
  if (editandoIndex.value === index) editandoIndex.value = null
  formCompra.detalles.splice(index, 1)
}

// ---------- Abrir Modal Compra ----------
const abrirModalCrear = () => {
  if (!can('COMPRAS', 'insertar')) return
  Object.keys(erroresForm).forEach(k => delete erroresForm[k])
  formCompra.numeroDocumento = ''
  formCompra.idProveedor = proveedores.value[0]?.idProveedor || ''
  formCompra.fechaCompra = new Date().toISOString().slice(0, 10)
  formCompra.detalles = []
  itemForm.idArticulo = ''
  itemForm.idAlmacenDestino = almacenes.value[0]?.idAlmacen || ''
  itemForm.cantidad = null
  itemForm.costoUnitario = null
  editandoIndex.value = null
  showModal.value = true
}

const validarFormularioCompra = () => {
  Object.keys(erroresForm).forEach(k => delete erroresForm[k])
  const doc = formCompra.numeroDocumento?.trim().toUpperCase() || ''

  if (!doc) {
    erroresForm.numeroDocumento = 'El número de factura o documento es obligatorio.'
  } else if (!TextRules.esCodigoValido(doc)) {
    erroresForm.numeroDocumento = 'Formato inválido (solo letras mayúsculas, números y guiones).'
  }

  if (!formCompra.idProveedor) {
    erroresForm.idProveedor = 'Selecciona el proveedor emisor.'
  }

  if (formCompra.detalles.length === 0) {
    erroresForm.general = 'Debes ingresar al menos un insumo en la compra.'
  }

  return Object.keys(erroresForm).length === 0
}

const solicitarConfirmacionGuardar = async () => {
  if (!validarFormularioCompra()) return

  const prov = proveedores.value.find(p => p.idProveedor === Number(formCompra.idProveedor))

  const confirmado = await pedirConfirmacion({
    variant: 'primary',
    title: '¿Registrar Compra?',
    message: 'La compra quedará asentada en estado Pendiente. No alterará el inventario físico hasta que se reciba la entrega en bodega.',
    detalles: [
      `Documento: ${formCompra.numeroDocumento.trim().toUpperCase()}`,
      `Proveedor: ${prov?.nombreProveedor || '—'}`,
      `Renglones: ${formCompra.detalles.length} materiales`,
      `Monto Total: ${formatMoneda(totalCompraForm.value)}`
    ],
    confirmText: 'Sí, registrar compra',
    cancelText: 'Volver a revisar'
  })

  if (!confirmado) return

  isSaving.value = true
  const payload = {
    numeroDocumento: formCompra.numeroDocumento.trim().toUpperCase(),
    idProveedor: Number(formCompra.idProveedor),
    fechaCompra: formCompra.fechaCompra,
    detalles: formCompra.detalles.map(d => ({
      idArticulo: d.idArticulo,
      idUnidadMedida: d.idUnidadMedida,
      cantidad: d.cantidad,
      costoUnitario: d.costoUnitario,
      idAlmacenDestino: d.idAlmacenDestino
    }))
  }

  try {
    await compraService.registrar(payload)
    showModal.value = false
    mostrarToast('success', 'Compra registrada exitosamente en estado Pendiente.')
    await cargarCompras()
  } catch (err) {
    const msg = err?.response?.data?.message || 'Error al procesar la compra.'
    erroresForm.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

// ---------- Recepción Física e Impacto en Kardex ----------
const solicitarRecepcion = async (c) => {
  if (!can('COMPRAS', 'modificar')) return

  const confirmado = await pedirConfirmacion({
    variant: 'primary',
    title: '¿Confirmar Recepción de Compra?',
    message: 'Esta acción dará ingreso físico inmediato a los insumos en las bodegas asignadas, generará los asientos formales en el KARDEX y actualizará el Costo Promedio Ponderado.',
    detalles: [
      `Documento: ${c.numeroDocumento}`,
      `Proveedor: ${c.nombreProveedor}`,
      `Total a ingresar: ${c.totalUnidades} unidades`,
      `Monto contable: ${formatMoneda(c.costoTotal)}`
    ],
    confirmText: 'Sí, ingresar a Bodega',
    cancelText: 'Cancelar'
  })

  if (!confirmado) return

  isSaving.value = true
  try {
    await compraService.recibir(c.idCompra)
    mostrarToast('success', 'Mercadería ingresada al inventario y Kardex actualizado con éxito.')
    await cargarCompras()
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo recibir la compra.')
  } finally {
    isSaving.value = false
  }
}

// ---------- Anular Compra ----------
const solicitarAnulacion = async (c) => {
  if (!can('COMPRAS', 'eliminar')) return
  const esRecibida = c.estadoCompra === 'Recibido'

  const confirmado = await pedirConfirmacion({
    variant: 'danger',
    title: '¿Anular esta Compra?',
    message: esRecibida
      ? 'La compra ya había ingresado a bodega. Al anularla, el sistema descargará el stock de existencias mediante un ajuste de Kardex.'
      : 'La compra pasará a estado Anulado y no se podrá procesar posteriormente.',
    detalles: [
      `Documento: ${c.numeroDocumento}`,
      `Proveedor: ${c.nombreProveedor}`,
      `Monto: ${formatMoneda(c.costoTotal)}`
    ],
    confirmText: 'Sí, anular compra',
    cancelText: 'Volver'
  })

  if (!confirmado) return

  isSaving.value = true
  try {
    await compraService.anular(c.idCompra)
    mostrarToast('success', 'Compra anulada correctamente.')
    await cargarCompras()
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo anular la compra.')
  } finally {
    isSaving.value = false
  }
}

// ---------- Inspección de Detalle ----------
const verDetalle = async (c) => {
  compraDetalle.value = null
  try {
    compraDetalle.value = await compraService.obtenerDetalle(c.idCompra)
    showDetalleModal.value = true
  } catch (err) {
    mostrarToast('error', 'No se pudo cargar el detalle de la compra.')
  }
}

// Escape
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) resolverConfirmacion(false)
  else if (showModal.value) showModal.value = false
  else if (showDetalleModal.value) showDetalleModal.value = false
}

onMounted(async () => {
  window.addEventListener('keydown', onKeydown)
  await cargarCompras()
  await cargarCatalogos()
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeydown)
  clearTimeout(busquedaTimeout)
})
</script>

<template>
  <MainLayout>
    <!-- Restricción Permiso -->
    <div v-if="!can('COMPRAS', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar el módulo de compras.</p>
    </div>

    <!-- Vista Principal -->
    <div v-else class="space-y-6">
      <!-- Encabezado con Franja Gradiente -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-lg font-black text-slate-800">Gestión de Compras y Abastecimiento</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de facturas de proveedores, recepción en bodega e impacto en Kardex</p>
        </div>

        <button
          v-if="can('COMPRAS', 'insertar')"
          @click="abrirModalCrear"
          class="inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:-translate-y-0.5 active:translate-y-0 transition-all focus:outline-none whitespace-nowrap"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
          Nueva Compra
        </button>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Total Compras</p>
            <p class="text-2xl font-black text-slate-800 mt-1 font-mono">{{ kpis.total }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-slate-100 text-slate-500 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-amber-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-amber-600 uppercase tracking-wide">Pendientes Entrega</p>
            <p class="text-2xl font-black text-amber-700 mt-1 font-mono">{{ kpis.pendientes }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-amber-100 text-amber-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-emerald-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-emerald-600 uppercase tracking-wide">Recibidas en Bodega</p>
            <p class="text-2xl font-black text-emerald-700 mt-1 font-mono">{{ kpis.recibidas }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-emerald-100 text-emerald-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-purple-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-purple-600 uppercase tracking-wide">Inversión Total</p>
            <p class="text-xl font-black text-purple-800 mt-1 font-mono">{{ formatMoneda(kpis.montoTotal) }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-purple-100 text-purple-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          </div>
        </div>
      </div>

      <!-- Barra de Filtros -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 space-y-3">
        <div class="flex flex-col md:flex-row md:items-center gap-3">
          <div class="relative flex-1">
            <input
              v-model="filtroBusqueda"
              @input="onBusquedaInput"
              type="text"
              placeholder="Buscar por # documento/factura o nombre de proveedor..."
              class="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition"
            />
            <svg class="w-4 h-4 text-slate-400 absolute left-3 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </div>

          <div class="flex flex-wrap gap-1.5">
            <button
              v-for="opcion in [{ v: '', l: 'Todos' }, ...ESTADOS.map(e => ({ v: e, l: e }))]"
              :key="opcion.v"
              @click="filtroEstado = opcion.v; cargarCompras()"
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

        <div class="flex flex-wrap items-center gap-3 pt-3 border-t border-slate-100 text-xs">
          <div>
            <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Proveedor</label>
            <select
              v-model="filtroProveedor"
              @change="cargarCompras"
              class="mt-1 block px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs font-semibold text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20"
            >
              <option value="">Todos los proveedores</option>
              <option v-for="p in proveedores" :key="p.idProveedor" :value="p.idProveedor">{{ p.nombreProveedor }}</option>
            </select>
          </div>
          <div>
            <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Desde</label>
            <input
              v-model="fechaDesde"
              @change="cargarCompras"
              type="date"
              class="mt-1 block px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs text-slate-700 focus:bg-white focus:outline-none"
            />
          </div>
          <div>
            <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Hasta</label>
            <input
              v-model="fechaHasta"
              @change="cargarCompras"
              type="date"
              class="mt-1 block px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs text-slate-700 focus:bg-white focus:outline-none"
            />
          </div>
          <button
            v-if="fechaDesde || fechaHasta || filtroProveedor"
            @click="fechaDesde = ''; fechaHasta = ''; filtroProveedor = ''; cargarCompras()"
            class="ml-auto text-[11px] font-bold text-slate-400 hover:text-purple-600 pt-4"
          >
            Limpiar filtros
          </button>
        </div>
      </div>

      <!-- Tabla de Datos -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <div v-if="isLoading" class="divide-y divide-slate-50" aria-busy="true">
          <div v-for="n in 5" :key="n" class="px-5 py-4 flex items-center gap-4 animate-pulse">
            <div class="flex-1 space-y-2">
              <div class="h-3 bg-slate-100 rounded w-28"></div>
              <div class="h-2.5 bg-slate-100 rounded w-40"></div>
            </div>
            <div class="h-6 bg-slate-100 rounded-lg w-24"></div>
            <div class="h-6 bg-slate-100 rounded-lg w-20"></div>
          </div>
        </div>

        <div v-else-if="compras.length === 0" class="text-center py-16 px-4">
          <div class="w-14 h-14 rounded-2xl bg-purple-50 text-purple-400 flex items-center justify-center mx-auto mb-3">
            <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" /></svg>
          </div>
          <p class="text-sm font-bold text-slate-600">No hay compras registradas con estos filtros</p>
          <p class="text-xs text-slate-400 mt-1">Registra una factura o nota de compra para abastecer el taller.</p>
        </div>

        <div v-else class="overflow-x-auto">
          <table class="w-full text-xs">
            <thead>
              <tr class="border-b-2 border-slate-100 text-left bg-slate-50/60">
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Factura / Documento</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Proveedor</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Fecha Emisión</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-center">Renglones</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Costo Total</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-center">Estado</th>
                <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="c in compras"
                :key="c.idCompra"
                class="border-b border-slate-50 last:border-0 hover:bg-purple-50/30 transition-colors"
              >
                <td class="px-5 py-3.5">
                  <p class="font-black text-slate-900 font-mono text-xs">{{ c.numeroDocumento }}</p>
                  <span class="text-[10px] text-slate-400 font-medium">Registrado por: {{ c.nombreUsuario }}</span>
                </td>

                <td class="px-5 py-3.5 font-bold text-slate-800 text-xs">
                  {{ c.nombreProveedor }}
                </td>

                <td class="px-5 py-3.5 text-slate-600 font-medium">
                  {{ formatFecha(c.fechaCompra) }}
                </td>

                <td class="px-5 py-3.5 text-center">
                  <span class="px-2.5 py-1 rounded-md text-[10px] font-bold bg-indigo-50 border border-indigo-200/80 text-indigo-700">
                    {{ c.totalRenglones }} ítem(s) ({{ c.totalUnidades }} uds)
                  </span>
                </td>

                <td class="px-5 py-3.5 text-right font-mono font-black text-slate-900 text-sm">
                  {{ formatMoneda(c.costoTotal) }}
                </td>

                <td class="px-5 py-3.5 text-center">
                  <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[10px] font-bold border', ESTADO_STYLES[c.estadoCompra]]">
                    <span :class="['w-1.5 h-1.5 rounded-full', ESTADO_DOT[c.estadoCompra]]"></span>
                    {{ c.estadoCompra }}
                  </span>
                </td>

                <td class="px-5 py-3.5 text-right">
                  <div class="flex items-center justify-end gap-1.5">
                    <button
                      @click="verDetalle(c)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-purple-700 bg-purple-50 hover:bg-purple-600 hover:text-white border border-purple-200 transition-all shadow-2xs"
                    >
                      Detalle
                    </button>

                    <!-- Botón Recibir en Bodega -->
                    <button
                      v-if="can('COMPRAS', 'modificar') && c.estadoCompra === 'Pendiente'"
                      @click="solicitarRecepcion(c)"
                      :disabled="isSaving"
                      class="px-2.5 py-1.5 rounded-lg bg-emerald-50 text-emerald-700 font-bold text-xs hover:bg-emerald-600 hover:text-white border border-emerald-200 transition-all shadow-2xs"
                      title="Dar ingreso a bodega y Kardex"
                    >
                      Recibir
                    </button>

                    <!-- Botón Anular -->
                    <button
                      v-if="can('COMPRAS', 'eliminar') && c.estadoCompra !== 'Anulado'"
                      @click="solicitarAnulacion(c)"
                      :disabled="isSaving"
                      class="px-2.5 py-1.5 rounded-lg bg-rose-50 text-rose-600 font-bold text-xs hover:bg-rose-600 hover:text-white border border-rose-200 transition-all shadow-2xs"
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

    <!-- Modal Formulario: Registrar Compra -->
    <Transition name="fade">
      <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-2xl shadow-2xl w-full max-w-3xl overflow-hidden border border-slate-100 max-h-[92vh] flex flex-col">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>
          
          <div class="px-6 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">Registrar Compra de Insumos</h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Captura la factura de abastecimiento y especifica las bodegas de destino</p>
            </div>
            <button @click="showModal = false" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <form @submit.prevent="solicitarConfirmacionGuardar" class="p-6 space-y-4 overflow-y-auto flex-1">
            <div v-if="erroresForm.general" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-bold rounded-xl px-4 py-3">
              {{ erroresForm.general }}
            </div>

            <!-- Encabezado de la Compra -->
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3.5">
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  No. Factura / Documento <span class="text-rose-500">*</span>
                </label>
                <input
                  v-model="formCompra.numeroDocumento"
                  @keypress="allowOnly.codigoInput($event)"
                  required
                  maxlength="50"
                  type="text"
                  placeholder="FAC-2026-904"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                />
                <p v-if="erroresForm.numeroDocumento" class="text-rose-600 text-[11px] font-bold mt-1">{{ erroresForm.numeroDocumento }}</p>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Proveedor <span class="text-rose-500">*</span>
                </label>
                <select
                  v-model="formCompra.idProveedor"
                  required
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20"
                >
                  <option value="" disabled>Selecciona el proveedor</option>
                  <option v-for="p in proveedores" :key="p.idProveedor" :value="p.idProveedor">{{ p.nombreProveedor }}</option>
                </select>
                <p v-if="erroresForm.idProveedor" class="text-rose-600 text-[11px] font-bold mt-1">{{ erroresForm.idProveedor }}</p>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Fecha Emisión <span class="text-rose-500">*</span>
                </label>
                <input
                  v-model="formCompra.fechaCompra"
                  required
                  type="date"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20"
                />
              </div>
            </div>

            <!-- Caja: Agregar Renglón de Material -->
            <div :class="['p-4 rounded-2xl border transition-all', editandoIndex !== null ? 'bg-purple-50/50 border-purple-300 ring-2 ring-purple-400/20' : 'bg-slate-50/80 border-slate-200']">
              <div class="flex items-center justify-between mb-3">
                <h4 class="text-xs font-black uppercase tracking-wider text-slate-700">
                  {{ editandoIndex !== null ? `✏️ Modificando Renglón #${editandoIndex + 1}` : 'Agregar Material a la Factura' }}
                </h4>
                <p v-if="erroresForm.renglon" class="text-rose-600 text-[11px] font-bold">{{ erroresForm.renglon }}</p>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-12 gap-2.5 items-end">
                <div class="sm:col-span-4">
                  <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Artículo / Tela</label>
                  <select
                    v-model="itemForm.idArticulo"
                    @change="onArticuloChange"
                    class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-800 outline-none focus:border-purple-500"
                  >
                    <option value="" disabled>Selecciona material...</option>
                    <option v-for="a in articulosComprables" :key="a.idArticulo" :value="a.idArticulo">
                      {{ a.nombreArticulo }} ({{ a.codigoArticulo }})
                    </option>
                  </select>
                </div>

                <div class="sm:col-span-3">
                  <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Bodega Destino</label>
                  <select
                    v-model="itemForm.idAlmacenDestino"
                    class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-800 outline-none focus:border-purple-500"
                  >
                    <option v-for="alm in almacenes" :key="alm.idAlmacen" :value="alm.idAlmacen">{{ alm.nombreAlmacen }}</option>
                  </select>
                </div>

                <div class="sm:col-span-2">
                  <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Cantidad</label>
                  <div class="relative">
                    <input
                      v-model.number="itemForm.cantidad"
                      type="number"
                      step="0.01"
                      min="0.01"
                      placeholder="0.00"
                      class="w-full px-2.5 py-2 bg-white border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 outline-none focus:border-purple-500"
                    />
                  </div>
                </div>

                <div class="sm:col-span-2">
                  <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Costo Unit. (Q)</label>
                  <input
                    v-model.number="itemForm.costoUnitario"
                    type="number"
                    step="0.0001"
                    min="0"
                    placeholder="0.00"
                    class="w-full px-2.5 py-2 bg-white border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 outline-none focus:border-purple-500"
                  />
                </div>

                <div class="sm:col-span-1">
                  <button
                    type="button"
                    @click="procesarRenglon"
                    class="w-full py-2 bg-gradient-to-tl from-purple-700 to-pink-500 hover:opacity-95 text-white font-bold text-xs rounded-xl shadow-md transition"
                  >
                    {{ editandoIndex !== null ? '✓' : '+' }}
                  </button>
                </div>
              </div>
            </div>

            <!-- Tabla de Renglones Agregados -->
            <div class="overflow-x-auto border border-slate-200 rounded-2xl">
              <table class="w-full text-left border-collapse">
                <thead>
                  <tr class="border-b border-slate-200 bg-slate-50 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                    <th class="py-2.5 px-3.5">Material</th>
                    <th class="py-2.5 px-3">Bodega Destino</th>
                    <th class="py-2.5 px-3 text-right">Cantidad</th>
                    <th class="py-2.5 px-3 text-right">Costo Unit.</th>
                    <th class="py-2.5 px-3 text-right">Subtotal</th>
                    <th class="py-2.5 px-3 text-center">Quitar</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100 text-xs">
                  <tr
                    v-for="(d, index) in formCompra.detalles"
                    :key="`${d.idArticulo}-${d.idAlmacenDestino}`"
                    @click="seleccionarParaEditarRenglon(index)"
                    :class="[
                      'cursor-pointer transition-colors',
                      editandoIndex === index ? 'bg-purple-100/70 border-l-4 border-l-purple-600 font-bold' : 'hover:bg-purple-50/30'
                    ]"
                  >
                    <td class="py-2.5 px-3.5 font-bold text-slate-800">
                      {{ d.nombreArticulo }} <span class="text-[10px] font-mono text-slate-400">({{ d.codigoArticulo }})</span>
                    </td>
                    <td class="py-2.5 px-3 text-slate-600 font-medium">{{ d.nombreAlmacenDestino }}</td>
                    <td class="py-2.5 px-3 text-right font-mono font-bold">{{ d.cantidad }} {{ d.codigoMedida }}</td>
                    <td class="py-2.5 px-3 text-right font-mono">{{ formatMoneda(d.costoUnitario) }}</td>
                    <td class="py-2.5 px-3 text-right font-mono font-black text-purple-700">{{ formatMoneda(d.subtotal) }}</td>
                    <td class="py-2.5 px-3 text-center" @click.stop>
                      <button type="button" @click="quitarRenglon(index)" class="p-1 rounded-lg text-rose-500 hover:bg-rose-600 hover:text-white transition font-bold">
                        ✕
                      </button>
                    </td>
                  </tr>
                  <tr v-if="formCompra.detalles.length === 0">
                    <td colspan="6" class="py-8 text-center text-slate-400 text-xs italic font-semibold">
                      No se han agregado renglones a esta factura. Selecciona un material arriba y presiona +.
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Total General -->
            <div class="flex items-center justify-between p-3.5 bg-purple-50/70 border border-purple-200 rounded-2xl">
              <span class="text-xs font-black text-slate-800 uppercase tracking-wide">Costo Total Factura:</span>
              <span class="text-base font-mono font-black text-purple-700">{{ formatMoneda(totalCompraForm) }}</span>
            </div>

            <!-- Footer -->
            <div class="flex justify-end gap-2.5 pt-3 border-t border-slate-100">
              <button
                type="button"
                @click="showModal = false"
                class="px-4 py-2.5 text-xs font-bold text-slate-600 hover:bg-slate-100 rounded-xl transition"
              >
                Cancelar
              </button>
              <button
                type="submit"
                :disabled="isSaving || formCompra.detalles.length === 0"
                class="px-5 py-2.5 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition disabled:opacity-50"
              >
                {{ isSaving ? 'Guardando...' : 'Revisar y Guardar' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Transition>

    <!-- Modal Detalle de Compra Registrada -->
    <Transition name="fade">
      <div v-if="showDetalleModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-2xl shadow-2xl w-full max-w-2xl overflow-hidden border border-slate-100 max-h-[85vh] flex flex-col">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">Factura {{ compraDetalle?.numeroDocumento }}</h3>
              <p class="text-xs text-slate-500 font-semibold">{{ compraDetalle?.nombreProveedor }} · Emisión: {{ formatFecha(compraDetalle?.fechaCompra) }}</p>
            </div>
            <span v-if="compraDetalle" :class="['px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-2xs', ESTADO_STYLES[compraDetalle.estadoCompra]]">
              {{ compraDetalle.estadoCompra }}
            </span>
          </div>

          <div class="my-4 overflow-y-auto px-6 flex-1">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="border-b border-slate-100 bg-slate-50/70 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                  <th class="py-2.5 px-3">Artículo / Tela</th>
                  <th class="py-2.5 px-3">Bodega Destino</th>
                  <th class="py-2.5 px-3 text-right">Cant.</th>
                  <th class="py-2.5 px-3 text-right">Costo Unit.</th>
                  <th class="py-2.5 px-3 text-right">Subtotal</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-for="d in compraDetalle?.detalles" :key="d.idDetalle" class="hover:bg-purple-50/20">
                  <td class="py-2.5 px-3 font-bold text-slate-800">
                    {{ d.nombreArticulo }} <span class="text-[10px] font-mono text-slate-400">({{ d.codigoArticulo }})</span>
                  </td>
                  <td class="py-2.5 px-3 text-slate-600 font-medium">{{ d.nombreAlmacenDestino }}</td>
                  <td class="py-2.5 px-3 text-right font-mono font-bold">{{ d.cantidad }} {{ d.codigoMedida }}</td>
                  <td class="py-2.5 px-3 text-right font-mono">{{ formatMoneda(d.costoUnitario) }}</td>
                  <td class="py-2.5 px-3 text-right font-mono font-black text-slate-900">{{ formatMoneda(d.subtotal) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="px-6 py-4 border-t border-slate-100 flex items-center justify-between bg-slate-50/50">
            <span class="text-xs font-black text-slate-700 uppercase">Total Liquidado:</span>
            <span class="text-base font-mono font-black text-purple-700">{{ formatMoneda(compraDetalle?.costoTotal) }}</span>
          </div>

          <div class="px-6 pb-4 flex justify-end">
            <button @click="showDetalleModal = false" class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition">
              Cerrar
            </button>
          </div>
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
              class="flex-1 px-4 py-2.5 text-xs font-bold text-slate-500 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors"
            >
              {{ confirmDialog.cancelText }}
            </button>
            <button
              ref="confirmBtnRef"
              type="button"
              @click="resolverConfirmacion(true)"
              :class="['flex-1 px-4 py-2.5 text-xs font-bold text-white rounded-xl shadow-md transition-all', CONFIRM_VARIANTS[confirmDialog.variant].button]"
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
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="TOAST_VARIANTS[t.type].icon" /></svg>
          </div>
          <div class="flex-1 min-w-0 pt-0.5">
            <p class="text-xs font-black text-slate-800">{{ TOAST_VARIANTS[t.type].title }}</p>
            <p class="text-[11px] font-medium text-slate-500 leading-snug mt-0.5">{{ t.message }}</p>
          </div>
          <button @click="quitarToast(t.id)" class="opacity-40 hover:opacity-100 transition-opacity">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
          <div :class="['toast-progress absolute bottom-0 left-0 h-1', TOAST_VARIANTS[t.type].bar]" :style="{ animationDuration: t.duracion + 'ms' }"></div>
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

.fade-enter-active .modal-card {
  animation: modal-in 0.25s ease-out;
}
@keyframes modal-in {
  from { transform: translateY(12px) scale(0.98); opacity: 0; }
  to { transform: translateY(0) scale(1); opacity: 1; }
}

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
</style>