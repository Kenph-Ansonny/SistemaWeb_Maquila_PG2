<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import kardexService from '../services/kardexService'
import articuloService from '../services/articuloService'
import almacenService from '../services/almacenService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly } from '../utils/validators'

const { can } = usePermissions()

// ---------- Pestañas de Vista ----------
const pestanaActiva = ref('movimientos') // 'movimientos' | 'existencias'

const TIPOS_MOVIMIENTO = [
  { val: '', label: 'Todos' },
  { val: 'ENTRADA_COMPRA', label: 'Compras' },
  { val: 'CONSUMO_PRODUCCION', label: 'Consumos Taller' },
  { val: 'ENTRADA_PRODUCIDO', label: 'Prendas Fabricadas' },
  { val: 'MERMA_CORTE', label: 'Retazos Merma' },
  { val: 'SALIDA_PEDIDO', label: 'Despachos' },
  { val: 'AJUSTE_INVENTARIO', label: 'Ajustes Manuales' }
]

const MOVIMIENTO_STYLES = {
  'ENTRADA_COMPRA': 'bg-emerald-100 text-emerald-800 border-emerald-300',
  'ENTRADA_PRODUCIDO': 'bg-sky-100 text-sky-800 border-sky-300',
  'MERMA_CORTE': 'bg-teal-100 text-teal-800 border-teal-300',
  'CONSUMO_PRODUCCION': 'bg-amber-100 text-amber-800 border-amber-300',
  'SALIDA_PEDIDO': 'bg-purple-100 text-purple-800 border-purple-300',
  'AJUSTE_INVENTARIO': 'bg-slate-200 text-slate-800 border-slate-300'
}

const MOVIMIENTO_DOT = {
  'ENTRADA_COMPRA': 'bg-emerald-500',
  'ENTRADA_PRODUCIDO': 'bg-sky-500',
  'MERMA_CORTE': 'bg-teal-500',
  'CONSUMO_PRODUCCION': 'bg-amber-500',
  'SALIDA_PEDIDO': 'bg-purple-500',
  'AJUSTE_INVENTARIO': 'bg-slate-500'
}

// ---------- Diálogo de Confirmación (Pop-in) ----------
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

// ---------- Estado Base ----------
const movimientos = ref([])
const existencias = ref([])
const articulos = ref([])
const almacenes = ref([])
const isLoading = ref(true)
const isSaving = ref(false)

// Filtros
const filtroBusqueda = ref('')
const filtroTipoMov = ref('')
const filtroAlmacen = ref('')
const fechaDesde = ref('')
const fechaHasta = ref('')
let busquedaTimeout = null

// Modales
const showAjusteModal = ref(false)
const showDetalleModal = ref(false)
const movimientoSeleccionado = ref(null)

const formAjuste = reactive({
  idArticulo: '',
  idAlmacen: '',
  tipoAjuste: 'entrada', // 'entrada' | 'salida'
  cantidad: null,
  motivo: ''
})
const erroresAjuste = reactive({})

const formatMoneda = (val) =>
  new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(val || 0)

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

// ---------- Carga de Datos ----------
const cargarMovimientos = async () => {
  if (!can('KARDEX', 'consultar')) {
    isLoading.value = false
    return
  }
  isLoading.value = true
  try {
    const data = await kardexService.obtenerMovimientos({
      tipoMovimiento: filtroTipoMov.value || undefined,
      idAlmacen: filtroAlmacen.value || undefined,
      fechaDesde: fechaDesde.value || undefined,
      fechaHasta: fechaHasta.value || undefined,
      busqueda: filtroBusqueda.value || undefined
    })
    movimientos.value = Array.isArray(data) ? data : (data?.data || [])
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudieron sincronizar los movimientos del Kardex.')
  } finally {
    isLoading.value = false
  }
}

const cargarExistencias = async () => {
  try {
    const data = await kardexService.obtenerExistencias()
    existencias.value = Array.isArray(data) ? data : (data?.data || [])
  } catch (err) {
    mostrarToast('error', 'No se pudieron consultar las existencias físicas.')
  }
}

const cargarCatalogos = async () => {
  try {
    const [resArticulos, resAlmacenes] = await Promise.all([
      articuloService.obtenerTodos(),
      almacenService.obtenerTodos()
    ])
    articulos.value = Array.isArray(resArticulos) ? resArticulos : (resArticulos?.data || [])
    almacenes.value = Array.isArray(resAlmacenes) ? resAlmacenes : (resAlmacenes?.data || [])
  } catch {
    mostrarToast('error', 'Error al consultar almacenes y artículos.')
  }
}

const onBusquedaInput = () => {
  clearTimeout(busquedaTimeout)
  busquedaTimeout = setTimeout(() => {
    if (pestanaActiva.value === 'movimientos') cargarMovimientos()
  }, 350)
}

// Control Teclado
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) resolverConfirmacion(false)
  else if (showAjusteModal.value) showAjusteModal.value = false
  else if (showDetalleModal.value) showDetalleModal.value = false
}

onMounted(async () => {
  window.addEventListener('keydown', onKeydown)
  await cargarMovimientos()
  await cargarExistencias()
  await cargarCatalogos()
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeydown)
  clearTimeout(busquedaTimeout)
})

// ---------- KPIs ----------
const kpis = computed(() => {
  const lista = movimientos.value
  return {
    totalMovimientos: lista.length,
    entradas: lista.filter(m => m.cantidad > 0).length,
    salidas: lista.filter(m => m.cantidad < 0).length,
    valorTotal: lista.reduce((acc, m) => acc + Number(m.montoTotalMovimiento || 0), 0)
  }
})

const articuloAjusteInfo = computed(() =>
  articulos.value.find(a => a.idArticulo === Number(formAjuste.idArticulo)) || null
)

// ---------- Ajuste Manual de Inventario ----------
const abrirModalAjuste = () => {
  if (!can('KARDEX', 'insertar')) return
  Object.keys(erroresAjuste).forEach(k => delete erroresAjuste[k])
  formAjuste.idArticulo = articulos.value[0]?.idArticulo || ''
  formAjuste.idAlmacen = almacenes.value[0]?.idAlmacen || ''
  formAjuste.tipoAjuste = 'entrada'
  formAjuste.cantidad = null
  formAjuste.motivo = ''
  showAjusteModal.value = true
}

const validarAjuste = () => {
  Object.keys(erroresAjuste).forEach(k => delete erroresAjuste[k])
  if (!formAjuste.idArticulo) erroresAjuste.idArticulo = 'Selecciona el artículo.'
  if (!formAjuste.idAlmacen) erroresAjuste.idAlmacen = 'Selecciona el almacén.'
  const cant = Number(formAjuste.cantidad)
  if (!cant || cant <= 0) erroresAjuste.cantidad = 'Ingresa una cantidad mayor a cero.'
  if (!formAjuste.motivo?.trim()) erroresAjuste.motivo = 'Ingresa el motivo técnico del ajuste.'
  return Object.keys(erroresAjuste).length === 0
}

const solicitarConfirmacionAjuste = async () => {
  if (!validarAjuste()) return

  const art = articuloAjusteInfo.value
  const alm = almacenes.value.find(a => a.idAlmacen === Number(formAjuste.idAlmacen))
  const esSalida = formAjuste.tipoAjuste === 'salida'

  const confirmado = await pedirConfirmacion({
    variant: esSalida ? 'danger' : 'primary',
    title: esSalida ? '¿Confirmar Salida por Ajuste?' : '¿Confirmar Entrada por Ajuste?',
    message: esSalida
      ? `Se descontarán ${formAjuste.cantidad} unidades del almacén "${alm?.nombreAlmacen}". Esta acción afectará el stock físico.`
      : `Se añadirán ${formAjuste.cantidad} unidades al almacén "${alm?.nombreAlmacen}".`,
    detalles: [
      `Artículo: ${art?.nombreArticulo} (${art?.codigoArticulo})`,
      `Almacén: ${alm?.nombreAlmacen}`,
      `Cantidad: ${esSalida ? '-' : '+'}${formAjuste.cantidad}`,
      `Motivo: ${formAjuste.motivo}`
    ],
    confirmText: 'Sí, aplicar ajuste',
    cancelText: 'Volver a revisar'
  })

  if (!confirmado) return

  isSaving.value = true
  const cantidadFirmada = esSalida ? -Math.abs(Number(formAjuste.cantidad)) : Math.abs(Number(formAjuste.cantidad))

  try {
    await kardexService.registrarAjuste({
      idArticulo: Number(formAjuste.idArticulo),
      idAlmacen: Number(formAjuste.idAlmacen),
      cantidadAjuste: cantidadFirmada,
      motivoAjuste: formAjuste.motivo.trim(),
      costoUnitario: art?.costoPromedio ?? 0
    })
    showAjusteModal.value = false
    mostrarToast('success', 'Ajuste de inventario asentado correctamente en el Kardex.')
    await cargarMovimientos()
    await cargarExistencias()
  } catch (err) {
    const msg = err?.response?.data?.message || 'Error al aplicar el ajuste de inventario.'
    erroresAjuste.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

const verDetalle = (m) => {
  movimientoSeleccionado.value = m
  showDetalleModal.value = true
}
</script>

<template>
  <MainLayout>
    <!-- Restricción Permiso -->
    <div v-if="!can('KARDEX', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar el libro de movimientos Kardex.</p>
    </div>

    <!-- Vista Principal -->
    <div v-else class="space-y-6">
      <!-- Encabezado con Franja Gradiente -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-lg font-black text-slate-800">Kardex Transaccional de Inventario</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Libro mayor de entradas, consumos de taller, retazos de corte y balance físico</p>
        </div>

        <div class="flex items-center gap-2">
          <!-- Toggle Pestañas -->
          <div class="inline-flex p-1 bg-slate-100 rounded-xl border border-slate-200">
            <button
              @click="pestanaActiva = 'movimientos'"
              :class="[
                'px-3.5 py-1.5 text-xs font-bold rounded-lg transition-all',
                pestanaActiva === 'movimientos' ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm' : 'text-slate-600 hover:text-slate-900'
              ]"
            >
              Movimientos
            </button>
            <button
              @click="pestanaActiva = 'existencias'"
              :class="[
                'px-3.5 py-1.5 text-xs font-bold rounded-lg transition-all',
                pestanaActiva === 'existencias' ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm' : 'text-slate-600 hover:text-slate-900'
              ]"
            >
              Existencias por Bodega
            </button>
          </div>

          <button
            v-if="can('KARDEX', 'insertar')"
            @click="abrirModalAjuste"
            class="inline-flex items-center justify-center gap-2 px-4 py-2 bg-slate-800 hover:bg-slate-900 text-white text-xs font-bold rounded-xl shadow-md transition-all hover:-translate-y-0.5"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
            Ajuste Manual
          </button>
        </div>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Total Asientos</p>
            <p class="text-2xl font-black text-slate-800 mt-1 font-mono">{{ kpis.totalMovimientos }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-slate-100 text-slate-500 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-emerald-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-emerald-600 uppercase tracking-wide">Entradas Físicas</p>
            <p class="text-2xl font-black text-emerald-700 mt-1 font-mono">{{ kpis.entradas }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-emerald-100 text-emerald-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 11l5-5m0 0l5 5m-5-5v12" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-amber-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-amber-600 uppercase tracking-wide">Salidas / Consumos</p>
            <p class="text-2xl font-black text-amber-700 mt-1 font-mono">{{ kpis.salidas }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-amber-100 text-amber-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 13l-5 5m0 0l-5-5m5 5V6" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border-2 border-purple-200 p-4 flex items-center justify-between gap-3">
          <div>
            <p class="text-[10px] font-bold text-purple-600 uppercase tracking-wide">Monto Movilizado</p>
            <p class="text-xl font-black text-purple-800 mt-1 font-mono">{{ formatMoneda(kpis.valorTotal) }}</p>
          </div>
          <div class="w-9 h-9 rounded-xl bg-purple-100 text-purple-600 flex items-center justify-center">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          </div>
        </div>
      </div>

      <!-- ================= PESTAÑA 1: MOVIMIENTOS KARDEX ================= -->
      <div v-if="pestanaActiva === 'movimientos'" class="space-y-4">
        <!-- Filtros -->
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 space-y-3">
          <div class="flex flex-col md:flex-row md:items-center gap-3">
            <div class="relative flex-1">
              <input
                v-model="filtroBusqueda"
                @input="onBusquedaInput"
                type="text"
                placeholder="Buscar por artículo, código de tela o referencia..."
                class="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition"
              />
              <svg class="w-4 h-4 text-slate-400 absolute left-3 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
            </div>

            <div class="flex flex-wrap gap-1.5">
              <button
                v-for="opcion in TIPOS_MOVIMIENTO"
                :key="opcion.val"
                @click="filtroTipoMov = opcion.val; cargarMovimientos()"
                :class="[
                  'px-3 py-1.5 rounded-lg text-[11px] font-bold transition-all',
                  filtroTipoMov === opcion.val
                    ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm shadow-purple-500/30'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                ]"
              >
                {{ opcion.label }}
              </button>
            </div>
          </div>

          <div class="flex flex-wrap items-center gap-3 pt-3 border-t border-slate-100 text-xs">
            <div>
              <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Bodega</label>
              <select
                v-model="filtroAlmacen"
                @change="cargarMovimientos"
                class="mt-1 block px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs font-semibold text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20"
              >
                <option value="">Todas las bodegas</option>
                <option v-for="a in almacenes" :key="a.idAlmacen" :value="a.idAlmacen">{{ a.nombreAlmacen }}</option>
              </select>
            </div>
            <div>
              <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Desde</label>
              <input
                v-model="fechaDesde"
                @change="cargarMovimientos"
                type="date"
                class="mt-1 block px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs text-slate-700 focus:bg-white focus:outline-none"
              />
            </div>
            <div>
              <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wide">Hasta</label>
              <input
                v-model="fechaHasta"
                @change="cargarMovimientos"
                type="date"
                class="mt-1 block px-3 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs text-slate-700 focus:bg-white focus:outline-none"
              />
            </div>
            <button
              v-if="fechaDesde || fechaHasta || filtroAlmacen"
              @click="fechaDesde = ''; fechaHasta = ''; filtroAlmacen = ''; cargarMovimientos()"
              class="ml-auto text-[11px] font-bold text-slate-400 hover:text-purple-600 pt-4"
            >
              Limpiar filtros
            </button>
          </div>
        </div>

        <!-- Tabla Libro Mayor -->
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
          <div v-if="isLoading" class="divide-y divide-slate-50" aria-busy="true">
            <div v-for="n in 6" :key="n" class="px-5 py-4 flex items-center gap-4 animate-pulse">
              <div class="flex-1 space-y-2">
                <div class="h-3 bg-slate-100 rounded w-32"></div>
                <div class="h-2.5 bg-slate-100 rounded w-48"></div>
              </div>
              <div class="h-6 bg-slate-100 rounded-lg w-28"></div>
              <div class="h-6 bg-slate-100 rounded-lg w-24"></div>
            </div>
          </div>

          <div v-else-if="movimientos.length === 0" class="text-center py-16 px-4">
            <p class="text-sm font-bold text-slate-600">No hay movimientos registrados en el Kardex</p>
            <p class="text-xs text-slate-400 mt-1">Los movimientos se registrarán al avanzar órdenes de producción o registrar mermas.</p>
          </div>

          <div v-else class="overflow-x-auto">
            <table class="w-full text-xs">
              <thead>
                <tr class="border-b-2 border-slate-100 text-left bg-slate-50/60">
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Fecha / Referencia</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Artículo / Insumo</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Bodega</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-center">Tipo Movimiento</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Cantidad</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Stock Posterior</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Costo Unit.</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="m in movimientos"
                  :key="m.idKardex"
                  class="border-b border-slate-50 last:border-0 hover:bg-purple-50/30 transition-colors"
                >
                  <td class="px-5 py-3.5">
                    <p class="font-bold text-slate-800">{{ formatFecha(m.fechaMovimiento) }}</p>
                    <span class="text-[10px] font-mono text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-200">
                      {{ m.tipoDocumentoReferencia }} #{{ m.idDocumentoReferencia }}
                    </span>
                  </td>

                  <td class="px-5 py-3.5">
                    <p class="font-bold text-slate-800">{{ m.nombreArticulo }}</p>
                    <p class="text-[10px] font-mono text-slate-400">{{ m.codigoArticulo }} · {{ m.tipoArticulo }}</p>
                  </td>

                  <td class="px-5 py-3.5 font-semibold text-slate-700">
                    {{ m.nombreAlmacen }}
                  </td>

                  <td class="px-5 py-3.5 text-center">
                    <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[10px] font-bold border', MOVIMIENTO_STYLES[m.tipoMovimiento]]">
                      <span :class="['w-1.5 h-1.5 rounded-full', MOVIMIENTO_DOT[m.tipoMovimiento]]"></span>
                      {{ m.tipoMovimiento }}
                    </span>
                  </td>

                  <!-- Cantidad firmada -->
                  <td class="px-5 py-3.5 text-right font-mono font-bold text-xs" :class="m.cantidad > 0 ? 'text-emerald-700' : 'text-rose-700'">
                    {{ m.cantidad > 0 ? '+' : '' }}{{ Number(m.cantidad).toFixed(2) }} {{ m.unidadMedida }}
                  </td>

                  <!-- Stock resultante -->
                  <td class="px-5 py-3.5 text-right font-mono font-bold text-slate-800">
                    {{ Number(m.stockPosterior).toFixed(2) }} {{ m.unidadMedida }}
                  </td>

                  <td class="px-5 py-3.5 text-right font-mono text-slate-600">
                    {{ formatMoneda(m.costoUnitario) }}
                  </td>

                  <td class="px-5 py-3.5 text-right">
                    <button
                      @click="verDetalle(m)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-purple-700 bg-purple-50 hover:bg-purple-600 hover:text-white border border-purple-200 transition-all shadow-2xs"
                    >
                      Detalle
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- ================= PESTAÑA 2: EXISTENCIAS CONSOLIDADAS ================= -->
      <div v-else class="space-y-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h3 class="text-sm font-black text-slate-800">Saldos Físicos de Inventario por Bodega</h3>
              <p class="text-xs text-slate-500 font-medium">Existencias actuales calculadas a partir del libro mayor de Kardex</p>
            </div>
            <button @click="cargarExistencias" class="text-xs font-bold text-purple-600 hover:text-purple-800">
              Actualizar Saldos
            </button>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-xs">
              <thead>
                <tr class="border-b-2 border-slate-100 text-left bg-slate-50/60">
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Artículo / Código</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Tipo</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Costo Promedio</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Stock Mínimo</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px] text-right">Stock Total</th>
                  <th class="px-5 py-3 font-bold text-slate-500 uppercase tracking-wide text-[10px]">Desglose por Bodega</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="e in existencias" :key="e.idArticulo" class="hover:bg-purple-50/20 transition-colors">
                  <td class="px-5 py-3.5">
                    <p class="font-bold text-slate-800">{{ e.nombreArticulo }}</p>
                    <p class="text-[10px] font-mono text-slate-400">{{ e.codigoArticulo }}</p>
                  </td>

                  <td class="px-5 py-3.5">
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-100 text-slate-700 border border-slate-200">
                      {{ e.tipoArticulo }}
                    </span>
                  </td>

                  <td class="px-5 py-3.5 text-right font-mono font-semibold text-slate-700">
                    {{ formatMoneda(e.costoPromedio) }}
                  </td>

                  <td class="px-5 py-3.5 text-right font-mono text-slate-500">
                    {{ Number(e.stockMinimo).toFixed(2) }} {{ e.unidadMedida }}
                  </td>

                  <!-- Stock Total con Alerta de Mínimo -->
                  <td class="px-5 py-3.5 text-right font-mono font-bold text-xs" :class="e.stockTotalEmpresa <= e.stockMinimo ? 'text-rose-600 font-black' : 'text-slate-900'">
                    {{ Number(e.stockTotalEmpresa).toFixed(2) }} {{ e.unidadMedida }}
                    <span v-if="e.stockTotalEmpresa <= e.stockMinimo" class="block text-[9px] text-rose-500 font-bold uppercase">
                      Bajo Mínimo
                    </span>
                  </td>

                  <!-- Desglose por Almacén -->
                  <td class="px-5 py-3.5">
                    <div class="flex flex-wrap gap-1.5">
                      <span
                        v-for="b in e.almacenes"
                        :key="b.idAlmacen"
                        class="px-2 py-0.5 rounded-lg text-[10px] font-bold bg-slate-100 border border-slate-200 text-slate-700"
                      >
                        {{ b.nombreAlmacen }}: <span class="font-mono text-purple-700">{{ Number(b.stockActual).toFixed(2) }}</span>
                      </span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Formulario: Ajuste Manual de Inventario -->
    <Transition name="fade">
      <div v-if="showAjusteModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h3 class="font-black text-slate-800 text-sm">Ajuste Manual de Inventario</h3>
              <p class="text-[11px] text-slate-400 font-medium mt-0.5">Corrige discrepancias físicas por inventario inicial, mermas o recuentos</p>
            </div>
            <button @click="showAjusteModal = false" class="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100" aria-label="Cerrar">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <form @submit.prevent="solicitarConfirmacionAjuste" class="px-6 py-5 space-y-4 max-h-[75vh] overflow-y-auto">
            <div v-if="erroresAjuste.general" class="flex items-start gap-2 bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold rounded-xl px-3 py-2" role="alert">
              <svg class="w-4 h-4 flex-shrink-0 mt-px" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              {{ erroresAjuste.general }}
            </div>

            <!-- Sentido del Ajuste -->
            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Sentido del Ajuste <span class="text-rose-500">*</span></label>
              <div class="grid grid-cols-2 gap-2 mt-1">
                <button
                  type="button"
                  @click="formAjuste.tipoAjuste = 'entrada'"
                  :class="[
                    'py-2 rounded-xl text-xs font-bold border transition-all',
                    formAjuste.tipoAjuste === 'entrada' ? 'bg-emerald-600 text-white border-transparent shadow-sm' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                  ]"
                >
                  + Entrada (Sobrante físico)
                </button>
                <button
                  type="button"
                  @click="formAjuste.tipoAjuste = 'salida'"
                  :class="[
                    'py-2 rounded-xl text-xs font-bold border transition-all',
                    formAjuste.tipoAjuste === 'salida' ? 'bg-rose-600 text-white border-transparent shadow-sm' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                  ]"
                >
                  - Salida (Faltante / Daño)
                </button>
              </div>
            </div>

            <!-- Selector de Artículo -->
            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Artículo / Insumo <span class="text-rose-500">*</span></label>
              <select
                v-model="formAjuste.idArticulo"
                required
                class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20"
              >
                <option v-for="a in articulos" :key="a.idArticulo" :value="a.idArticulo">
                  {{ a.nombreArticulo }} ({{ a.codigoArticulo }})
                </option>
              </select>
              <p v-if="erroresAjuste.idArticulo" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresAjuste.idArticulo }}</p>
            </div>

            <!-- Selector de Almacén -->
            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Almacén / Bodega <span class="text-rose-500">*</span></label>
              <select
                v-model="formAjuste.idAlmacen"
                required
                class="mt-1 w-full px-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20"
              >
                <option v-for="a in almacenes" :key="a.idAlmacen" :value="a.idAlmacen">{{ a.nombreAlmacen }}</option>
              </select>
              <p v-if="erroresAjuste.idAlmacen" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresAjuste.idAlmacen }}</p>
            </div>

            <!-- Cantidad -->
            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Cantidad a Ajustar <span class="text-rose-500">*</span></label>
              <input
                v-model.number="formAjuste.cantidad"
                type="number"
                min="0.01"
                step="0.01"
                placeholder="0.00"
                required
                class="mt-1 w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20"
              />
              <p v-if="erroresAjuste.cantidad" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresAjuste.cantidad }}</p>
            </div>

            <!-- Motivo -->
            <div>
              <label class="text-[11px] font-bold text-slate-500 uppercase tracking-wide">Motivo Técnico del Ajuste <span class="text-rose-500">*</span></label>
              <textarea
                v-model="formAjuste.motivo"
                @keypress="allowOnly.descripcionInput($event)"
                rows="2"
                required
                maxlength="250"
                placeholder="Ej: Conteo físico semestral, merma no registrada o regularización de stock inicial..."
                class="mt-1 w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20"
              ></textarea>
              <p v-if="erroresAjuste.motivo" class="text-rose-500 text-[11px] font-semibold mt-1">{{ erroresAjuste.motivo }}</p>
            </div>

            <div class="px-6 py-4 border-t border-slate-100 flex items-center justify-end gap-2 -mx-6 -mb-5 bg-slate-50/50">
              <button
                type="button"
                @click="showAjusteModal = false"
                :disabled="isSaving"
                class="px-4 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100 rounded-xl transition-colors disabled:opacity-50"
              >
                Cancelar
              </button>
              <button
                type="submit"
                :disabled="isSaving"
                class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 transition-all hover:shadow-lg disabled:opacity-50"
              >
                Revisar y Guardar
              </button>
            </div>
          </form>
        </div>
      </div>
    </Transition>

    <!-- Modal: Detalle de Movimiento de Kardex -->
    <Transition name="fade">
      <div v-if="showDetalleModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden border border-slate-100">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-800 text-base">Asiento de Kardex #{{ movimientoSeleccionado?.idKardex }}</h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Trazabilidad inmutable de inventario</p>
            </div>
            <button @click="showDetalleModal = false" class="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <div v-if="movimientoSeleccionado" class="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
            <div class="bg-slate-50 border border-slate-200 rounded-2xl p-4 text-xs space-y-2">
              <div class="flex justify-between">
                <span class="text-slate-500 font-bold uppercase text-[10px]">Artículo:</span>
                <span class="font-bold text-slate-900">{{ movimientoSeleccionado.nombreArticulo }} ({{ movimientoSeleccionado.codigoArticulo }})</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500 font-bold uppercase text-[10px]">Almacén / Bodega:</span>
                <span class="font-bold text-slate-800">{{ movimientoSeleccionado.nombreAlmacen }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500 font-bold uppercase text-[10px]">Tipo de Asiento:</span>
                <span :class="['px-2 py-0.5 rounded text-[10px] font-bold border', MOVIMIENTO_STYLES[movimientoSeleccionado.tipoMovimiento]]">
                  {{ movimientoSeleccionado.tipoMovimiento }}
                </span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500 font-bold uppercase text-[10px]">Documento Referencia:</span>
                <span class="font-mono font-bold text-purple-700">{{ movimientoSeleccionado.tipoDocumentoReferencia }} #{{ movimientoSeleccionado.idDocumentoReferencia }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500 font-bold uppercase text-[10px]">Usuario Responsable:</span>
                <span class="font-semibold text-slate-700">{{ movimientoSeleccionado.nombreUsuario }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-500 font-bold uppercase text-[10px]">Fecha del Asiento:</span>
                <span class="font-semibold text-slate-700">{{ formatFecha(movimientoSeleccionado.fechaMovimiento) }}</span>
              </div>
            </div>

            <!-- Evolución del Stock -->
            <div class="grid grid-cols-3 gap-2 text-center">
              <div class="p-3 bg-slate-50 rounded-xl border border-slate-200">
                <span class="block text-[10px] font-bold text-slate-400 uppercase">Stock Anterior</span>
                <span class="font-mono font-bold text-slate-700 text-sm">{{ Number(movimientoSeleccionado.stockAnterior).toFixed(2) }}</span>
              </div>
              <div class="p-3 rounded-xl border" :class="movimientoSeleccionado.cantidad > 0 ? 'bg-emerald-50 border-emerald-200 text-emerald-800' : 'bg-rose-50 border-rose-200 text-rose-800'">
                <span class="block text-[10px] font-bold uppercase">Movimiento</span>
                <span class="font-mono font-black text-sm">{{ movimientoSeleccionado.cantidad > 0 ? '+' : '' }}{{ Number(movimientoSeleccionado.cantidad).toFixed(2) }}</span>
              </div>
              <div class="p-3 bg-purple-50 rounded-xl border border-purple-200 text-purple-900">
                <span class="block text-[10px] font-bold uppercase">Stock Resultante</span>
                <span class="font-mono font-black text-sm">{{ Number(movimientoSeleccionado.stockPosterior).toFixed(2) }}</span>
              </div>
            </div>

            <!-- Valorización -->
            <div class="flex items-center justify-between p-3 bg-slate-50 rounded-xl border border-slate-200 text-xs">
              <span class="font-bold text-slate-600">Impacto Monetario Total:</span>
              <span class="font-mono font-black text-purple-700 text-sm">{{ formatMoneda(movimientoSeleccionado.montoTotalMovimiento) }}</span>
            </div>
          </div>

          <div class="px-6 py-4 border-t border-slate-100 flex items-center justify-end bg-slate-50/50">
            <button @click="showDetalleModal = false" class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition">
              Cerrar
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Diálogo de Confirmación (Pop-in) -->
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