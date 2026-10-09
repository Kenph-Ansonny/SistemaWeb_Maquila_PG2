<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import pedidoService from '../services/pedidoService'
import clienteService from '../services/clienteService'
import articuloService from '../services/articuloService'
import recetaService from '../services/recetaService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

const ESTADOS = ['Registrado', 'En Produccion', 'Finalizado', 'Entregado', 'Cancelado']

const TRANSICIONES = {
  'Registrado': ['En Produccion', 'Cancelado'],
  'En Produccion': ['Finalizado', 'Cancelado'],
  'Finalizado': ['Entregado'],
  'Entregado': [],
  'Cancelado': []
}

const ESTADO_STYLES = {
  'Registrado': 'bg-slate-100 text-slate-700 border-slate-200',
  'En Produccion': 'bg-indigo-50 text-indigo-700 border-indigo-200',
  'Finalizado': 'bg-amber-50 text-amber-700 border-amber-200',
  'Entregado': 'bg-emerald-50 text-emerald-700 border-emerald-200',
  'Cancelado': 'bg-rose-50 text-rose-700 border-rose-200'
}

const isSaving = ref(false)
const isLoading = ref(true)

// ---------- Datos base ----------
const pedidos = ref([])
const clientes = ref([])
const articulos = ref([])
const recetas = ref([])

// ---------- Filtros ----------
const filtroBusqueda = ref('')
const filtroEstado = ref('')
let busquedaTimeout = null

// ---------- Modales de Sistema ----------
const confirmModal = ref({ show: false, title: '', message: '', action: null, type: 'warning' })
const alertModal = ref({ show: false, title: '', message: '', type: 'success' })

const showAlert = (title, message, type = 'success') => {
  alertModal.value = { show: true, title, message, type }
}

// ---------- Modal: Crear / Editar Pedido ----------
const showModal = ref(false)
const isEditing = ref(false)
const currentId = ref(null)
const pedidoOriginal = ref(null)

const formPedido = reactive({
  numeroPedido: '',
  idCliente: '',
  fechaEstimadaEntrega: '',
  detalles: []
})

// Puntero de edición en tabla interna (-1 o null si está agregando)
const editandoIndex = ref(null)

const itemLineaForm = reactive({
  idArticuloPrenda: '',
  idReceta: '',
  cantidad: 1,
  precioUnitarioAcordado: 0
})

// ---------- Modal: Confirmar cambios (diff) ----------
const showConfirmCambios = ref(false)

// ---------- Modal: Ver detalle ----------
const showDetalleModal = ref(false)
const pedidoDetalle = ref(null)

// ---------- Modal: Cambiar estado ----------
const showEstadoModal = ref(false)
const pedidoParaEstado = ref(null)
const nuevoEstadoSeleccionado = ref('')

// Fecha mínima permitida (Hoy)
const hoyISO = computed(() => new Date().toISOString().slice(0, 10))

// ---------- Carga de datos ----------
const cargarPedidos = async () => {
  if (!can('PEDIDOS', 'consultar')) {
    isLoading.value = false
    return
  }
  isLoading.value = true
  try {
    const res = await pedidoService.obtenerTodos({
      estado: filtroEstado.value || undefined,
      busqueda: filtroBusqueda.value || undefined
    })
    pedidos.value = Array.isArray(res) ? res : (res?.data || [])
  } catch (err) {
    showAlert('Error de Sincronización', err.response?.data?.message || 'No se pudieron obtener los pedidos desde el servidor.', 'error')
  } finally {
    isLoading.value = false
  }
}

const cargarDatosBase = async () => {
  try {
    const [clientesData, resArticulos, resRecetas] = await Promise.all([
      clienteService.obtenerOpciones(),
      articuloService.obtenerTodos(),
      recetaService.obtenerTodos()
    ])
    clientes.value = Array.isArray(clientesData) ? clientesData : (clientesData?.data || [])
    articulos.value = Array.isArray(resArticulos) ? resArticulos : (resArticulos?.data || [])
    recetas.value = Array.isArray(resRecetas) ? resRecetas : (resRecetas?.data || [])
  } catch {
    showAlert('Error de Sincronización', 'No se pudieron cargar los catálogos auxiliares.', 'error')
  }
}

const onBusquedaInput = () => {
  clearTimeout(busquedaTimeout)
  busquedaTimeout = setTimeout(cargarPedidos, 350)
}

const setFiltroEstado = (estado) => {
  filtroEstado.value = estado
  cargarPedidos()
}

onMounted(() => {
  cargarPedidos()
  if (can('PEDIDOS', 'insertar') || can('PEDIDOS', 'modificar')) {
    cargarDatosBase()
  }
})

// ---------- Computados generales ----------
const kpis = computed(() => {
  const lista = pedidos.value
  return {
    total: lista.length,
    registrados: lista.filter(p => p.estadoPedido === 'Registrado').length,
    enProduccion: lista.filter(p => p.estadoPedido === 'En Produccion').length,
    finalizados: lista.filter(p => p.estadoPedido === 'Finalizado' || p.estadoPedido === 'Entregado').length,
    cancelados: lista.filter(p => p.estadoPedido === 'Cancelado').length
  }
})

const prendasTerminadas = computed(() =>
  articulos.value.filter(a => a.tipoArticulo === 'PrendaTerminada' && a.estadoArticulo)
)

const recetasDePrenda = computed(() => {
  if (!itemLineaForm.idArticuloPrenda) return []
  return recetas.value.filter(r => r.idArticuloPrenda === itemLineaForm.idArticuloPrenda && r.estadoReceta)
})

const prendaSeleccionadaInfo = computed(() =>
  articulos.value.find(a => a.idArticulo === itemLineaForm.idArticuloPrenda) || null
)

const totalFormPedido = computed(() =>
  formPedido.detalles.reduce((acc, d) => acc + (d.cantidad * d.precioUnitarioAcordado), 0)
)

// ---------- Utilidades ----------
const formatFecha = (valor) => {
  if (!valor) return '—'
  return new Date(valor).toLocaleDateString('es-GT', { day: '2-digit', month: 'short', year: 'numeric' })
}

const formatMoneda = (val) => new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(val || 0)

const puedeEditarOEliminar = (p) => p.estadoPedido === 'Registrado' && p.totalOrdenesProduccion === 0

// ---------- Gestión de Líneas en Formulario ----------
const onPrendaChange = () => {
  itemLineaForm.idReceta = recetasDePrenda.value[0]?.idReceta || ''
  const prenda = prendaSeleccionadaInfo.value
  itemLineaForm.precioUnitarioAcordado = prenda ? Number((prenda.costoPromedio || 0).toFixed(2)) : 0
}

const seleccionarLineaParaEditar = (index) => {
  editandoIndex.value = index
  const linea = formPedido.detalles[index]
  itemLineaForm.idArticuloPrenda = linea.idArticuloPrenda
  itemLineaForm.idReceta = linea.idReceta
  itemLineaForm.cantidad = linea.cantidad
  itemLineaForm.precioUnitarioAcordado = linea.precioUnitarioAcordado
}

const cancelarEdicionLinea = () => {
  editandoIndex.value = null
  itemLineaForm.idArticuloPrenda = prendasTerminadas.value[0]?.idArticulo || ''
  onPrendaChange()
  itemLineaForm.cantidad = 1
}

const procesarLinea = () => {
  const { idArticuloPrenda, idReceta, cantidad, precioUnitarioAcordado } = itemLineaForm

  if (!idArticuloPrenda) {
    showAlert('Campo Requerido', 'Selecciona la prenda terminada a confeccionar.', 'error')
    return
  }
  if (!idReceta) {
    showAlert('Campo Requerido', 'Selecciona la ficha técnica correspondiente.', 'error')
    return
  }
  if (!cantidad || cantidad <= 0) {
    showAlert('Cantidad Inválida', 'La cantidad requerida debe ser mayor a cero.', 'error')
    return
  }
  if (precioUnitarioAcordado === null || precioUnitarioAcordado === undefined || Number(precioUnitarioAcordado) <= 0) {
    showAlert('Precio Inválido', 'El precio acordado debe ser mayor a cero.', 'error')
    return
  }

  const prenda = articulos.value.find(a => a.idArticulo === idArticuloPrenda)
  const receta = recetas.value.find(r => r.idReceta === idReceta)

  if (editandoIndex.value !== null) {
    // Modo Edición de Fila existente
    const duplicado = formPedido.detalles.some(
      (d, idx) => idx !== editandoIndex.value && d.idArticuloPrenda === idArticuloPrenda && d.idReceta === idReceta
    )
    if (duplicado) {
      showAlert('Línea Duplicada', 'Ya existe otra fila con esta misma prenda y ficha técnica.', 'error')
      return
    }

    formPedido.detalles[editandoIndex.value] = {
      idArticuloPrenda,
      nombreArticulo: prenda?.nombreArticulo || '—',
      idReceta,
      nombreReceta: receta?.nombreReceta || '—',
      cantidad: Number(cantidad),
      precioUnitarioAcordado: Number(precioUnitarioAcordado)
    }

    showAlert('Fila Actualizada', 'Los datos de la prenda se actualizaron en la tabla.', 'success')
    cancelarEdicionLinea()
  } else {
    // Modo Inserción de Nueva Fila
    const yaExiste = formPedido.detalles.some(d => d.idArticuloPrenda === idArticuloPrenda && d.idReceta === idReceta)
    if (yaExiste) {
      showAlert('Línea Duplicada', 'Esta combinación ya está en la tabla. Selecciónala para modificarla.', 'error')
      return
    }

    formPedido.detalles.push({
      idArticuloPrenda,
      nombreArticulo: prenda?.nombreArticulo || '—',
      idReceta,
      nombreReceta: receta?.nombreReceta || '—',
      cantidad: Number(cantidad),
      precioUnitarioAcordado: Number(precioUnitarioAcordado)
    })

    itemLineaForm.cantidad = 1
    itemLineaForm.precioUnitarioAcordado = 0
  }
}

const quitarLinea = (index) => {
  if (editandoIndex.value === index) {
    cancelarEdicionLinea()
  }
  formPedido.detalles.splice(index, 1)
}

// ---------- Abrir Modales Creación / Edición ----------
const abrirModalCrear = async () => {
  if (!can('PEDIDOS', 'insertar')) return

  if (prendasTerminadas.value.length === 0) {
    showAlert('Prendas Inexistentes', 'Debes tener al menos una prenda terminada activa registrada.', 'error')
    return
  }
  if (clientes.value.length === 0) {
    showAlert('Clientes Inexistentes', 'Debes tener al menos un cliente activo en el catálogo.', 'error')
    return
  }

  isEditing.value = false
  currentId.value = null
  pedidoOriginal.value = null
  editandoIndex.value = null

  formPedido.numeroPedido = ''
  formPedido.idCliente = clientes.value[0]?.idCliente || ''
  formPedido.fechaEstimadaEntrega = ''
  formPedido.detalles = []

  itemLineaForm.idArticuloPrenda = prendasTerminadas.value[0]?.idArticulo || ''
  onPrendaChange()

  showModal.value = true

  try {
    const res = await pedidoService.obtenerSiguienteNumero()
    const correlativo = res?.data?.siguienteNumero || res?.siguienteNumero
    if (correlativo) formPedido.numeroPedido = correlativo
  } catch {
    // Autogeneración opcional
  }
}

const abrirModalEditar = async (p) => {
  if (!can('PEDIDOS', 'modificar')) return
  if (!puedeEditarOEliminar(p)) {
    showAlert('Acción No Permitida', 'Solo se pueden editar pedidos en estado "Registrado" sin órdenes de producción asignadas.', 'error')
    return
  }

  try {
    const res = await pedidoService.obtenerDetalle(p.idPedido)
    const data = res?.data || res

    isEditing.value = true
    currentId.value = data.idPedido
    editandoIndex.value = null

    const detallesMapeados = data.detalles.map(d => ({
      idArticuloPrenda: d.idArticuloPrenda,
      nombreArticulo: d.nombreArticulo,
      idReceta: d.idReceta,
      nombreReceta: d.nombreReceta,
      cantidad: d.cantidad,
      precioUnitarioAcordado: d.precioUnitarioAcordado
    }))

    formPedido.numeroPedido = data.numeroPedido
    formPedido.idCliente = data.idCliente
    formPedido.fechaEstimadaEntrega = data.fechaEstimadaEntrega ? data.fechaEstimadaEntrega.slice(0, 10) : ''
    formPedido.detalles = detallesMapeados.map(d => ({ ...d }))

    pedidoOriginal.value = {
      numeroPedido: data.numeroPedido,
      idCliente: data.idCliente,
      fechaEstimadaEntrega: formPedido.fechaEstimadaEntrega,
      detalles: detallesMapeados.map(d => ({ ...d }))
    }

    itemLineaForm.idArticuloPrenda = prendasTerminadas.value[0]?.idArticulo || ''
    onPrendaChange()

    showModal.value = true
  } catch (err) {
    showAlert('Error', err.response?.data?.message || 'No se pudo cargar la información del pedido.', 'error')
  }
}

const cerrarModalPedido = () => {
  if (isSaving.value) return
  showModal.value = false
  editandoIndex.value = null
}

// ---------- Validación Estricta + Diff ----------
const validarFormPedido = () => {
  const numero = formPedido.numeroPedido?.trim().toUpperCase() || ''

  if (!TextRules.esCodigoValido(numero)) {
    showAlert('Número Inválido', 'El número de pedido solo admite mayúsculas, números y guiones (3 a 30 caracteres).', 'error')
    return false
  }
  if (!formPedido.idCliente) {
    showAlert('Cliente Obligatorio', 'Selecciona el cliente solicitante.', 'error')
    return false
  }
  // Validación estricta de fecha solicitada
  if (!formPedido.fechaEstimadaEntrega) {
    showAlert('Fecha de Entrega Requerida', 'La fecha estimada de entrega es obligatoria para planificar la producción.', 'error')
    return false
  }
  if (formPedido.fechaEstimadaEntrega < hoyISO.value) {
    showAlert('Fecha No Válida', 'La fecha estimada de entrega no puede ser anterior al día de hoy.', 'error')
    return false
  }
  if (formPedido.detalles.length === 0) {
    showAlert('Prendas Requeridas', 'Debes incluir al menos una prenda en el pedido.', 'error')
    return false
  }

  return true
}

const camposDiff = computed(() => {
  if (!pedidoOriginal.value) return []
  const cambios = []
  const numeroNuevo = formPedido.numeroPedido?.trim().toUpperCase() || ''

  if (numeroNuevo !== pedidoOriginal.value.numeroPedido) {
    cambios.push({ campo: 'Número de Pedido', anterior: pedidoOriginal.value.numeroPedido, nuevo: numeroNuevo })
  }
  if (Number(formPedido.idCliente) !== pedidoOriginal.value.idCliente) {
    const anterior = clientes.value.find(c => c.idCliente === pedidoOriginal.value.idCliente)?.nombreCliente || '—'
    const nuevo = clientes.value.find(c => c.idCliente === Number(formPedido.idCliente))?.nombreCliente || '—'
    cambios.push({ campo: 'Cliente', anterior, nuevo })
  }
  if ((formPedido.fechaEstimadaEntrega || '') !== (pedidoOriginal.value.fechaEstimadaEntrega || '')) {
    cambios.push({
      campo: 'Fecha Estimada de Entrega',
      anterior: pedidoOriginal.value.fechaEstimadaEntrega ? formatFecha(pedidoOriginal.value.fechaEstimadaEntrega) : 'Sin definir',
      nuevo: formPedido.fechaEstimadaEntrega ? formatFecha(formPedido.fechaEstimadaEntrega) : 'Sin definir'
    })
  }
  return cambios
})

const lineasDiff = computed(() => {
  if (!pedidoOriginal.value) {
    return formPedido.detalles.map(d => ({ ...d, tipo: 'nueva' }))
  }

  const originales = pedidoOriginal.value.detalles
  const resultado = []

  formPedido.detalles.forEach(linea => {
    const match = originales.find(o => o.idArticuloPrenda === linea.idArticuloPrenda && o.idReceta === linea.idReceta)
    if (!match) {
      resultado.push({ ...linea, tipo: 'nueva' })
    } else if (match.cantidad !== linea.cantidad || match.precioUnitarioAcordado !== linea.precioUnitarioAcordado) {
      resultado.push({ ...linea, tipo: 'modificada', cantidadAnterior: match.cantidad, precioAnterior: match.precioUnitarioAcordado })
    } else {
      resultado.push({ ...linea, tipo: 'sinCambios' })
    }
  })

  originales.forEach(o => {
    const sigueExistiendo = formPedido.detalles.some(l => l.idArticuloPrenda === o.idArticuloPrenda && l.idReceta === o.idReceta)
    if (!sigueExistiendo) {
      resultado.push({ ...o, tipo: 'eliminada' })
    }
  })

  return resultado
})

const hayDiferencias = computed(() => {
  if (!pedidoOriginal.value) return true
  return camposDiff.value.length > 0 || lineasDiff.value.some(l => l.tipo !== 'sinCambios')
})

const solicitarConfirmacionGuardar = () => {
  if (!validarFormPedido()) return
  showConfirmCambios.value = true
}

const confirmarYGuardar = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const payload = {
    numeroPedido: formPedido.numeroPedido.trim().toUpperCase(),
    idCliente: Number(formPedido.idCliente),
    fechaEstimadaEntrega: formPedido.fechaEstimadaEntrega || null,
    detalles: formPedido.detalles.map(d => ({
      idArticuloPrenda: d.idArticuloPrenda,
      idReceta: d.idReceta,
      cantidad: d.cantidad,
      precioUnitarioAcordado: d.precioUnitarioAcordado
    }))
  }

  try {
    if (isEditing.value) {
      await pedidoService.actualizar(currentId.value, payload)
      showAlert('Modificación Exitosa', 'El pedido ha sido actualizado correctamente.')
    } else {
      await pedidoService.crear(payload)
      showAlert('Registro Exitoso', 'El pedido fue registrado en el catálogo.')
    }
    showConfirmCambios.value = false
    showModal.value = false
    cargarPedidos()
  } catch (err) {
    showAlert('Error al Procesar', err.response?.data?.message || 'Ocurrió un fallo en el servidor.', 'error')
  } finally {
    isSaving.value = false
  }
}

// ---------- Detalle de Pedido ----------
const verDetalle = async (p) => {
  pedidoDetalle.value = null
  try {
    const res = await pedidoService.obtenerDetalle(p.idPedido)
    pedidoDetalle.value = res?.data || res
    showDetalleModal.value = true
  } catch (err) {
    showAlert('Error', err.response?.data?.message || 'No se pudo obtener el detalle del pedido.', 'error')
  }
}

// ---------- Cambio de Estado con Confirmación ----------
const abrirModalEstado = (p) => {
  if (!can('PEDIDOS', 'modificar')) return
  pedidoParaEstado.value = p
  nuevoEstadoSeleccionado.value = ''
  showEstadoModal.value = true
}

const cerrarModalEstado = () => {
  if (isSaving.value) return
  showEstadoModal.value = false
  pedidoParaEstado.value = null
}

const solicitarConfirmacionEstado = () => {
  if (!nuevoEstadoSeleccionado.value || isSaving.value) return
  const estadoDestino = nuevoEstadoSeleccionado.value
  const esCancelado = estadoDestino === 'Cancelado'

  confirmModal.value = {
    show: true,
    title: esCancelado ? '¿Cancelar este pedido?' : '¿Confirmar cambio de estado?',
    message: esCancelado
      ? `El pedido "${pedidoParaEstado.value.numeroPedido}" cambiará a CANCELADO. Esta acción detendrá su ciclo operativo.`
      : `El pedido "${pedidoParaEstado.value.numeroPedido}" avanzará de "${pedidoParaEstado.value.estadoPedido}" a "${estadoDestino}". ¿Deseas aplicar el cambio?`,
    type: esCancelado ? 'danger' : 'warning',
    action: async () => {
      confirmModal.value.show = false
      await ejecutarCambioEstado()
    }
  }
}

const ejecutarCambioEstado = async () => {
  isSaving.value = true
  try {
    const res = await pedidoService.cambiarEstado(pedidoParaEstado.value.idPedido, nuevoEstadoSeleccionado.value)
    showAlert('Estado Actualizado', res?.data?.message || res?.message || `El pedido pasó a estado '${nuevoEstadoSeleccionado.value}'.`, 'success')
    showEstadoModal.value = false
    pedidoParaEstado.value = null
    await cargarPedidos()
  } catch (err) {
    showAlert('Error al Cambiar Estado', err.response?.data?.message || 'No se pudo cambiar el estado del pedido.', 'error')
  } finally {
    isSaving.value = false
  }
}

// ---------- Eliminar ----------
const solicitarEliminar = (p) => {
  if (!can('PEDIDOS', 'eliminar')) return
  if (!puedeEditarOEliminar(p)) {
    showAlert('Acción Bloqueada', 'Solo se pueden eliminar pedidos en estado "Registrado" sin órdenes de producción asignadas.', 'error')
    return
  }

  confirmModal.value = {
    show: true,
    title: '¿Eliminar Pedido?',
    message: `El pedido "${p.numeroPedido}" de ${p.nombreCliente} se eliminará permanentemente del sistema.`,
    type: 'danger',
    action: async () => {
      confirmModal.value.show = false
      try {
        const res = await pedidoService.eliminar(p.idPedido)
        showAlert('Pedido Eliminado', res?.data?.message || res?.message || 'Pedido eliminado exitosamente.')
        cargarPedidos()
      } catch (err) {
        showAlert('Error', err.response?.data?.message || 'No se pudo eliminar el pedido.', 'error')
      }
    }
  }
}
</script>

<template>
  <MainLayout>
    <!-- Modal Notificación Global -->
    <Transition name="fade">
      <div v-if="alertModal.show" class="fixed inset-0 z-[70] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
        <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
          <div :class="['w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3 shadow-md', alertModal.type === 'success' ? 'bg-emerald-50 text-emerald-600 border border-emerald-200' : 'bg-rose-50 text-rose-600 border border-rose-200']">
            <svg v-if="alertModal.type === 'success'" class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7" />
            </svg>
            <svg v-else class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>
          <h3 class="text-base font-black text-slate-900">{{ alertModal.title }}</h3>
          <p class="text-xs text-slate-600 font-medium mt-1 mb-6 leading-relaxed">{{ alertModal.message }}</p>
          <button @click="alertModal.show = false" class="w-full py-2.5 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md hover:shadow-lg transition-all">
            Aceptar
          </button>
        </div>
      </div>
    </Transition>

    <!-- Modal Confirmación Global -->
    <Transition name="fade">
      <div v-if="confirmModal.show" class="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
        <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
          <div :class="['w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3 shadow-md', confirmModal.type === 'danger' ? 'bg-rose-50 text-rose-600 border border-rose-200' : 'bg-amber-50 text-amber-600 border border-amber-200']">
            <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>
          <h3 class="text-base font-black text-slate-900">{{ confirmModal.title }}</h3>
          <p class="text-xs text-slate-600 font-medium mt-1 mb-6 leading-relaxed">{{ confirmModal.message }}</p>
          <div class="flex justify-center gap-2.5">
            <button @click="confirmModal.show = false" class="flex-1 py-2.5 text-xs font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition">
              Cancelar
            </button>
            <button @click="confirmModal.action" :class="['flex-1 py-2.5 text-xs font-bold text-white rounded-xl shadow-md transition', confirmModal.type === 'danger' ? 'bg-rose-600 hover:bg-rose-700' : 'bg-purple-600 hover:bg-purple-700']">
              Confirmar
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Restricción de Permiso -->
    <div v-if="!can('PEDIDOS', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-14 h-14 rounded-2xl bg-rose-50 text-rose-600 flex items-center justify-center mx-auto mb-3 border border-rose-200 shadow-sm">
        <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
      </div>
      <h3 class="text-base font-black text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-500 font-medium mt-1">No cuentas con autorización para consultar el módulo de pedidos.</p>
    </div>

    <!-- Vista Principal -->
    <div v-else class="space-y-5">
      <!-- Encabezado con Botón -->
      <div class="bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Pedidos de Maquila</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de pedidos de clientes, asignación de prendas y trazabilidad hacia producción</p>
        </div>
        <button
          v-if="can('PEDIDOS', 'insertar')"
          @click="abrirModalCrear"
          class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:scale-[1.01] active:scale-[0.98] transition-all whitespace-nowrap"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
          </svg>
          Nuevo Pedido
        </button>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-5 gap-3.5">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
          <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Total</p>
          <p class="text-2xl font-black text-slate-900 mt-1 font-mono">{{ kpis.total }}</p>
        </div>
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
          <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Registrados</p>
          <p class="text-2xl font-black text-slate-700 mt-1 font-mono">{{ kpis.registrados }}</p>
        </div>
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
          <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">En Producción</p>
          <p class="text-2xl font-black text-indigo-600 mt-1 font-mono">{{ kpis.enProduccion }}</p>
        </div>
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
          <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Finalizados</p>
          <p class="text-2xl font-black text-emerald-600 mt-1 font-mono">{{ kpis.finalizados }}</p>
        </div>
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4">
          <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Cancelados</p>
          <p class="text-2xl font-black text-rose-600 mt-1 font-mono">{{ kpis.cancelados }}</p>
        </div>
      </div>

      <!-- Filtros -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex flex-col md:flex-row md:items-center gap-3">
        <div class="relative flex-1">
          <input
            v-model="filtroBusqueda"
            @input="onBusquedaInput"
            type="text"
            placeholder="Buscar por número de pedido o cliente..."
            class="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
          />
          <svg class="w-4 h-4 text-slate-400 absolute left-3.5 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        <div class="flex flex-wrap gap-1.5 bg-slate-50 p-1 rounded-xl border border-slate-200">
          <button
            v-for="opcion in [{ v: '', l: 'Todos' }, ...ESTADOS.map(e => ({ v: e, l: e }))]"
            :key="opcion.v"
            @click="setFiltroEstado(opcion.v)"
            :class="[
              'px-3 py-1.5 rounded-lg text-xs font-bold transition-all',
              filtroEstado === opcion.v
                ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/60'
            ]"
          >
            {{ opcion.l }}
          </button>
        </div>
      </div>

      <!-- Tabla de Datos Principal -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <div v-if="isLoading" class="flex flex-col items-center justify-center py-20">
          <svg class="animate-spin w-8 h-8 text-purple-600" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"></path>
          </svg>
          <p class="text-xs font-bold text-slate-600 mt-3">Cargando pedidos de maquila...</p>
        </div>

        <div v-else-if="pedidos.length === 0" class="text-center py-20 px-4">
          <p class="text-base font-black text-slate-800">No se encontraron pedidos con estos filtros</p>
          <p class="text-xs text-slate-500 font-medium mt-1">Registra un nuevo pedido para iniciar el ciclo productivo.</p>
        </div>

        <div v-else class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 bg-slate-50/70 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                <th class="py-3.5 px-5">Pedido</th>
                <th class="py-3.5 px-4">Cliente</th>
                <th class="py-3.5 px-4">Emisión / Entrega Est.</th>
                <th class="py-3.5 px-4 text-center">Prendas</th>
                <th class="py-3.5 px-4 text-right">Total Acordado</th>
                <th class="py-3.5 px-4 text-center">Estado</th>
                <th class="py-3.5 px-5 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="p in pedidos" :key="p.idPedido" class="hover:bg-purple-50/20 transition-colors">
                <!-- Pedido -->
                <td class="py-3.5 px-5">
                  <span class="block font-black text-slate-900 font-mono text-[13px]">{{ p.numeroPedido }}</span>
                  <span class="text-[10px] font-bold text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-200 inline-block mt-0.5">
                    {{ p.cantidadLineas }} línea(s)
                  </span>
                </td>

                <!-- Cliente -->
                <td class="py-3.5 px-4 font-bold text-slate-800 text-[13px]">
                  {{ p.nombreCliente }}
                </td>

                <!-- Fechas con Alta Visibilidad -->
                <td class="py-3.5 px-4">
                  <div class="space-y-1">
                    <div class="flex items-center gap-1.5 text-slate-900 font-bold text-xs">
                      <svg class="w-3.5 h-3.5 text-purple-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                      </svg>
                      <span>{{ formatFecha(p.fechaEmision) }}</span>
                    </div>
                    <div v-if="p.fechaEstimadaEntrega" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-bold bg-indigo-50 text-indigo-700 border border-indigo-200 shadow-2xs">
                      <span class="text-indigo-500 font-black">Entrega:</span> {{ formatFecha(p.fechaEstimadaEntrega) }}
                    </div>
                    <div v-else class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-bold bg-amber-50 text-amber-700 border border-amber-200">
                      <span>Sin fecha definida</span>
                    </div>
                  </div>
                </td>

                <!-- Prendas -->
                <td class="py-3.5 px-4 text-center">
                  <span class="px-2.5 py-1 rounded-lg text-xs font-black bg-indigo-50 border border-indigo-200 text-indigo-700 font-mono shadow-2xs">
                    {{ p.totalPrendas }} uds.
                  </span>
                </td>

                <!-- Total -->
                <td class="py-3.5 px-4 text-right font-mono font-black text-slate-900 text-sm">
                  {{ formatMoneda(p.totalMonto) }}
                </td>

                <!-- Estado -->
                <td class="py-3.5 px-4 text-center">
                  <span :class="['px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-2xs', ESTADO_STYLES[p.estadoPedido]]">
                    {{ p.estadoPedido }}
                  </span>
                </td>

                <!-- Acciones -->
                <td class="py-3.5 px-5 text-right">
                  <div class="flex items-center justify-end gap-1.5 flex-wrap">
                    <button
                      @click="verDetalle(p)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-purple-700 bg-purple-50 hover:bg-purple-600 hover:text-white border border-purple-200 transition-all shadow-2xs"
                    >
                      Detalle
                    </button>
                    <button
                      v-if="can('PEDIDOS', 'modificar') && TRANSICIONES[p.estadoPedido]?.length > 0"
                      @click="abrirModalEstado(p)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-indigo-700 bg-indigo-50 hover:bg-indigo-600 hover:text-white border border-indigo-200 transition-all shadow-2xs"
                    >
                      Estado
                    </button>
                    <button
                      v-if="can('PEDIDOS', 'modificar') && puedeEditarOEliminar(p)"
                      @click="abrirModalEditar(p)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-slate-700 bg-slate-100 hover:bg-purple-600 hover:text-white border border-slate-200 transition-all shadow-2xs"
                    >
                      Editar
                    </button>
                    <button
                      v-if="can('PEDIDOS', 'eliminar') && puedeEditarOEliminar(p)"
                      @click="solicitarEliminar(p)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-rose-700 bg-rose-50 hover:bg-rose-600 hover:text-white border border-rose-200 transition-all shadow-2xs"
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

    <!-- Modal Formulario: Crear / Editar Pedido -->
    <Transition name="fade">
      <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-3xl overflow-hidden border border-slate-100 animate-scale-in max-h-[92vh] flex flex-col">
          <!-- Encabezado Modal -->
          <div class="px-6 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">
                {{ isEditing ? 'Editar Pedido de Maquila' : 'Nuevo Pedido de Maquila' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Define los datos del cliente, la fecha de entrega y las prendas a confeccionar</p>
            </div>
            <button @click="cerrarModalPedido" :disabled="isSaving" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <!-- Cuerpo con Scroll -->
          <form @submit.prevent="solicitarConfirmacionGuardar" class="p-6 space-y-4 overflow-y-auto flex-1">
            <!-- Encabezado del Pedido -->
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3.5">
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Número de Pedido <span class="text-rose-500">*</span>
                </label>
                <input
                  v-model="formPedido.numeroPedido"
                  @keypress="allowOnly.codigoInput($event)"
                  required
                  maxlength="30"
                  type="text"
                  placeholder="PED-2026-0001"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                />
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Cliente <span class="text-rose-500">*</span>
                </label>
                <select
                  v-model="formPedido.idCliente"
                  required
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                >
                  <option value="" disabled>Selecciona un cliente</option>
                  <option v-for="c in clientes" :key="c.idCliente" :value="c.idCliente">
                    {{ c.nombreCliente }}
                  </option>
                </select>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Fecha Estimada Entrega <span class="text-rose-500">*</span>
                </label>
                <input
                  v-model="formPedido.fechaEstimadaEntrega"
                  :min="hoyISO"
                  required
                  type="date"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                />
              </div>
            </div>

            <!-- Caja: Agregar / Modificar Línea -->
            <div :class="['p-4 rounded-2xl border transition-all', editandoIndex !== null ? 'bg-purple-50/50 border-purple-300 ring-2 ring-purple-400/20' : 'bg-slate-50/80 border-slate-200']">
              <div class="flex items-center justify-between mb-3">
                <h4 class="text-xs font-black uppercase tracking-wider" :class="editandoIndex !== null ? 'text-purple-800' : 'text-slate-700'">
                  {{ editandoIndex !== null ? `✏️ Modificando Fila #${editandoIndex + 1}` : 'Agregar Prenda al Pedido' }}
                </h4>
                <span v-if="editandoIndex !== null" class="text-[10px] font-bold text-purple-700 bg-purple-100 px-2 py-0.5 rounded-full border border-purple-200">
                  Modo Edición de Fila Activo
                </span>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-12 gap-2.5 items-end">
                <div class="sm:col-span-4">
                  <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Prenda Terminada</label>
                  <select
                    v-model="itemLineaForm.idArticuloPrenda"
                    @change="onPrendaChange"
                    class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-800 outline-none focus:border-purple-500"
                  >
                    <option v-for="pt in prendasTerminadas" :key="pt.idArticulo" :value="pt.idArticulo">
                      {{ pt.nombreArticulo }} ({{ pt.codigoArticulo }})
                    </option>
                  </select>
                </div>

                <div class="sm:col-span-3">
                  <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Ficha Técnica</label>
                  <select
                    v-model="itemLineaForm.idReceta"
                    class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-800 outline-none focus:border-purple-500"
                  >
                    <option v-if="recetasDePrenda.length === 0" value="" disabled>Sin fichas técnicas activas</option>
                    <option v-for="r in recetasDePrenda" :key="r.idReceta" :value="r.idReceta">
                      {{ r.nombreReceta }}
                    </option>
                  </select>
                </div>

                <div class="sm:col-span-2">
                  <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Cantidad</label>
                  <input
                    v-model.number="itemLineaForm.cantidad"
                    type="number"
                    min="1"
                    step="1"
                    class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 outline-none focus:border-purple-500"
                  />
                </div>

                <div class="sm:col-span-2">
                  <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Precio Unit. (Q)</label>
                  <input
                    v-model.number="itemLineaForm.precioUnitarioAcordado"
                    type="number"
                    min="0.01"
                    step="0.01"
                    class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 outline-none focus:border-purple-500"
                  />
                </div>

                <div class="sm:col-span-1 flex gap-1">
                  <button
                    type="button"
                    @click="procesarLinea"
                    :class="[
                      'w-full py-2 font-bold text-xs rounded-xl shadow-md transition-all text-white',
                      editandoIndex !== null ? 'bg-indigo-600 hover:bg-indigo-700' : 'bg-gradient-to-tl from-purple-700 to-pink-500'
                    ]"
                    :title="editandoIndex !== null ? 'Guardar cambios de la fila' : 'Agregar prenda a la lista'"
                  >
                    {{ editandoIndex !== null ? '✓' : '+' }}
                  </button>
                  <button
                    v-if="editandoIndex !== null"
                    type="button"
                    @click="cancelarEdicionLinea"
                    class="px-2 py-2 bg-slate-200 hover:bg-slate-300 text-slate-700 font-bold text-xs rounded-xl transition"
                    title="Cancelar edición de fila"
                  >
                    ✕
                  </button>
                </div>
              </div>
            </div>

            <!-- Tabla Interna de Prendas con Clic para Editar -->
            <div class="overflow-x-auto border border-slate-200 rounded-2xl">
              <table class="w-full text-left border-collapse">
                <thead>
                  <tr class="border-b border-slate-200 bg-slate-50 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                    <th class="py-2.5 px-3.5">Prenda</th>
                    <th class="py-2.5 px-3">Receta / BOM</th>
                    <th class="py-2.5 px-3 text-right">Cantidad</th>
                    <th class="py-2.5 px-3 text-right">Precio Unit.</th>
                    <th class="py-2.5 px-3 text-right">Subtotal</th>
                    <th class="py-2.5 px-3 text-center">Acciones</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100 text-xs">
                  <tr
                    v-for="(d, index) in formPedido.detalles"
                    :key="`${d.idArticuloPrenda}-${d.idReceta}`"
                    @click="seleccionarLineaParaEditar(index)"
                    :class="[
                      'cursor-pointer transition-colors',
                      editandoIndex === index
                        ? 'bg-purple-100/70 border-l-4 border-l-purple-600 font-bold'
                        : 'hover:bg-purple-50/30'
                    ]"
                  >
                    <td class="py-2.5 px-3.5 font-bold text-slate-800">
                      <div class="flex items-center gap-2">
                        <span>{{ d.nombreArticulo }}</span>
                        <span v-if="editandoIndex === index" class="px-1.5 py-0.5 rounded text-[9px] font-black uppercase bg-purple-600 text-white shadow-2xs">
                          Editando
                        </span>
                      </div>
                    </td>
                    <td class="py-2.5 px-3 text-slate-600 font-medium">{{ d.nombreReceta }}</td>
                    <td class="py-2.5 px-3 text-right font-mono font-bold text-slate-900">{{ d.cantidad }}</td>
                    <td class="py-2.5 px-3 text-right font-mono text-slate-700">{{ formatMoneda(d.precioUnitarioAcordado) }}</td>
                    <td class="py-2.5 px-3 text-right font-mono font-black text-purple-700">{{ formatMoneda(d.cantidad * d.precioUnitarioAcordado) }}</td>
                    <td class="py-2.5 px-3 text-center" @click.stop>
                      <button
                        type="button"
                        @click="quitarLinea(index)"
                        class="p-1 rounded-lg text-rose-500 hover:text-white hover:bg-rose-600 font-bold transition"
                        title="Eliminar fila"
                      >
                        ✕
                      </button>
                    </td>
                  </tr>
                  <tr v-if="formPedido.detalles.length === 0">
                    <td colspan="6" class="py-8 text-center text-slate-400 text-xs italic font-semibold">
                      No se han agregado prendas a este pedido. Selecciona una prenda arriba y presiona +.
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Total General del Formulario -->
            <div class="flex items-center justify-between p-3.5 bg-purple-50/70 border border-purple-200 rounded-2xl">
              <span class="text-xs font-black text-slate-800 uppercase tracking-wide">Monto Total del Pedido:</span>
              <span class="text-base font-mono font-black text-purple-700">{{ formatMoneda(totalFormPedido) }}</span>
            </div>

            <!-- Footer del Formulario -->
            <div class="flex justify-end gap-2.5 pt-3 border-t border-slate-100">
              <button
                type="button"
                @click="cerrarModalPedido"
                class="px-4 py-2.5 text-xs font-bold text-slate-600 hover:bg-slate-100 rounded-xl transition"
              >
                Cancelar
              </button>
              <button
                type="submit"
                class="px-5 py-2.5 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg transition-all"
              >
                {{ isEditing ? 'Revisar Cambios' : 'Revisar y Registrar' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Transition>

    <!-- Modal: Confirmar Cambios (Diff Visual Comparativo) -->
    <Transition name="fade">
      <div v-if="showConfirmCambios" class="fixed inset-0 z-[65] flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
        <div class="bg-white rounded-3xl p-6 sm:p-7 shadow-2xl w-full max-w-xl border border-slate-100 animate-scale-in max-h-[88vh] flex flex-col">
          <div class="text-center pb-4 border-b border-slate-100">
            <div class="w-14 h-14 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center mx-auto mb-2 border border-purple-100">
              <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <h3 class="text-lg font-black text-slate-900">
              {{ isEditing ? '¿Guardar modificaciones del pedido?' : '¿Confirmar registro del pedido?' }}
            </h3>
            <p class="text-xs text-slate-500 font-medium mt-0.5">Revisa el desglose antes de guardar. Quedará registrado en la bitácora.</p>
          </div>

          <div class="flex-1 overflow-y-auto py-4 space-y-4">
            <!-- Resumen Cabecera -->
            <div class="bg-slate-50 border border-slate-200 rounded-2xl p-4 text-xs space-y-1.5 font-medium">
              <p><span class="font-bold text-slate-700">Número de Pedido:</span> {{ formPedido.numeroPedido }}</p>
              <p><span class="font-bold text-slate-700">Cliente:</span> {{ clientes.find(c => c.idCliente === Number(formPedido.idCliente))?.nombreCliente }}</p>
              <p><span class="font-bold text-slate-700">Entrega Estimada:</span> {{ formatFecha(formPedido.fechaEstimadaEntrega) }}</p>
              <p><span class="font-bold text-purple-700">Total:</span> <span class="font-mono font-bold">{{ formatMoneda(totalFormPedido) }}</span></p>
            </div>

            <!-- Diff Campos Cabecera (Edición) -->
            <div v-if="isEditing && camposDiff.length > 0">
              <h4 class="text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-2">Modificaciones en el Encabezado</h4>
              <table class="w-full text-xs border border-slate-200 rounded-xl overflow-hidden">
                <thead class="bg-slate-50 text-[10px] font-bold text-slate-500 uppercase">
                  <tr>
                    <th class="py-2 px-3 text-left">Campo</th>
                    <th class="py-2 px-3 text-left">Anterior</th>
                    <th class="py-2 px-3 text-left">Nuevo</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100">
                  <tr v-for="c in camposDiff" :key="c.campo">
                    <td class="py-2 px-3 font-bold text-slate-700">{{ c.campo }}</td>
                    <td class="py-2 px-3 text-rose-500 line-through font-medium">{{ c.anterior }}</td>
                    <td class="py-2 px-3 text-emerald-600 font-bold">{{ c.nuevo }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Diff Líneas / Artículos -->
            <div>
              <h4 class="text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-2">
                {{ isEditing ? 'Prendas Modificadas en el Pedido' : 'Prendas a Registrar' }}
              </h4>
              <div class="space-y-1.5">
                <div
                  v-for="(l, idx) in lineasDiff"
                  :key="idx"
                  :class="[
                    'flex items-center justify-between px-3.5 py-2.5 rounded-xl text-xs border',
                    l.tipo === 'nueva' ? 'bg-emerald-50 border-emerald-200' :
                    l.tipo === 'eliminada' ? 'bg-rose-50 border-rose-200' :
                    l.tipo === 'modificada' ? 'bg-amber-50 border-amber-200' :
                    'bg-slate-50 border-slate-200'
                  ]"
                >
                  <div :class="l.tipo === 'eliminada' ? 'line-through text-slate-400' : 'text-slate-800'">
                    <span class="font-bold">{{ l.nombreArticulo }}</span>
                    <span class="text-slate-500"> — {{ l.nombreReceta }}</span>
                    <span v-if="l.tipo === 'modificada'" class="block text-[11px] font-medium mt-0.5">
                      Cant: <span class="line-through text-slate-400">{{ l.cantidadAnterior }}</span> → <span class="font-bold text-amber-700">{{ l.cantidad }}</span>
                      &nbsp;|&nbsp; Precio: <span class="line-through text-slate-400">{{ formatMoneda(l.precioAnterior) }}</span> → <span class="font-bold text-amber-700">{{ formatMoneda(l.precioUnitarioAcordado) }}</span>
                    </span>
                    <span v-else class="block text-[11px] text-slate-500 mt-0.5">
                      {{ l.cantidad }} uds. × {{ formatMoneda(l.precioUnitarioAcordado) }}
                    </span>
                  </div>
                  <span
                    :class="[
                      'px-2 py-0.5 rounded-md text-[10px] font-black uppercase tracking-wider ml-2 shrink-0',
                      l.tipo === 'nueva' ? 'bg-emerald-200 text-emerald-800' :
                      l.tipo === 'eliminada' ? 'bg-rose-200 text-rose-800' :
                      l.tipo === 'modificada' ? 'bg-amber-200 text-amber-800' :
                      'bg-slate-200 text-slate-700'
                    ]"
                  >
                    {{ l.tipo === 'nueva' ? 'Nueva' : l.tipo === 'eliminada' ? 'Eliminada' : l.tipo === 'modificada' ? 'Modificada' : 'Sin cambios' }}
                  </span>
                </div>
              </div>
            </div>

            <p v-if="isEditing && !hayDiferencias" class="text-center text-xs font-bold text-slate-400 italic py-2">
              No se detectaron modificaciones en el pedido original.
            </p>
          </div>

          <!-- Botones Acción -->
          <div class="flex items-center gap-3 pt-3 border-t border-slate-100">
            <button
              type="button"
              @click="showConfirmCambios = false"
              :disabled="isSaving"
              class="flex-1 py-2.5 text-xs font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition"
            >
              Volver a revisar
            </button>
            <button
              type="button"
              @click="confirmarYGuardar"
              :disabled="isSaving || (isEditing && !hayDiferencias)"
              class="flex-1 py-2.5 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg transition-all disabled:opacity-50"
            >
              {{ isSaving ? 'Guardando...' : 'Sí, guardar cambios' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Modal: Ver Detalle Completo -->
    <Transition name="fade">
      <div v-if="showDetalleModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
        <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-2xl border border-slate-100 max-h-[85vh] flex flex-col animate-scale-in">
          <div class="flex items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <h3 class="text-base font-black text-slate-900">Pedido {{ pedidoDetalle?.numeroPedido }}</h3>
              <p class="text-xs text-slate-500 font-semibold">{{ pedidoDetalle?.nombreCliente }} · {{ pedidoDetalle?.telefonoCliente || 'Sin teléfono' }}</p>
            </div>
            <span v-if="pedidoDetalle" :class="['px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-2xs', ESTADO_STYLES[pedidoDetalle.estadoPedido]]">
              {{ pedidoDetalle.estadoPedido }}
            </span>
          </div>

          <div class="my-4 overflow-y-auto pr-1 flex-1">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="border-b border-slate-100 bg-slate-50/70 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                  <th class="py-2.5 px-3">Prenda</th>
                  <th class="py-2.5 px-3">Receta / BOM</th>
                  <th class="py-2.5 px-3 text-right">Cant.</th>
                  <th class="py-2.5 px-3 text-right">Precio</th>
                  <th class="py-2.5 px-3 text-right">Subtotal</th>
                  <th class="py-2.5 px-3 text-right">Pendiente Programar</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-for="d in pedidoDetalle?.detalles" :key="d.idDetalle" class="hover:bg-purple-50/20">
                  <td class="py-2.5 px-3 font-bold text-slate-800">
                    <span class="block">{{ d.nombreArticulo }}</span>
                    <span class="text-[10px] text-slate-400" v-if="d.talla || d.color">
                      <span v-if="d.talla">T: {{ d.talla }}</span> <span v-if="d.color">C: {{ d.color }}</span>
                    </span>
                  </td>
                  <td class="py-2.5 px-3 text-slate-600 font-medium">{{ d.nombreReceta }}</td>
                  <td class="py-2.5 px-3 text-right font-mono font-bold">{{ d.cantidad }}</td>
                  <td class="py-2.5 px-3 text-right font-mono">{{ formatMoneda(d.precioUnitarioAcordado) }}</td>
                  <td class="py-2.5 px-3 text-right font-mono font-black text-slate-900">{{ formatMoneda(d.subtotal) }}</td>
                  <td class="py-2.5 px-3 text-right">
                    <span :class="['px-2 py-0.5 rounded-md text-[10px] font-bold border', d.cantidadPendienteProgramar > 0 ? 'bg-amber-50 text-amber-700 border-amber-200' : 'bg-emerald-50 text-emerald-700 border-emerald-200']">
                      {{ d.cantidadPendienteProgramar }} / {{ d.cantidad }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="flex items-center justify-between pt-3 border-t border-slate-100">
            <span class="text-xs font-black text-slate-700 uppercase">Total del Pedido:</span>
            <span class="text-base font-mono font-black text-purple-700">{{ formatMoneda(pedidoDetalle?.totalMonto) }}</span>
          </div>

          <div class="flex justify-end pt-3">
            <button @click="showDetalleModal = false" class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition">
              Cerrar
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Modal: Cambiar Estado con Confirmación Previa -->
    <Transition name="fade">
      <div v-if="showEstadoModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
        <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 animate-scale-in">
          <h3 class="text-base font-black text-slate-900 mb-0.5">Cambiar Estado del Pedido</h3>
          <p class="text-xs text-slate-500 font-semibold mb-4">{{ pedidoParaEstado?.numeroPedido }} · {{ pedidoParaEstado?.nombreCliente }}</p>

          <div class="flex items-center justify-center gap-2.5 mb-5 text-xs">
            <span :class="['px-3 py-1.5 rounded-xl font-bold border', ESTADO_STYLES[pedidoParaEstado?.estadoPedido]]">
              {{ pedidoParaEstado?.estadoPedido }}
            </span>
            <svg class="w-4 h-4 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3" />
            </svg>
            <span :class="['px-3 py-1.5 rounded-xl font-bold border', nuevoEstadoSeleccionado ? ESTADO_STYLES[nuevoEstadoSeleccionado] : 'bg-slate-50 text-slate-400 border-dashed border-slate-300']">
              {{ nuevoEstadoSeleccionado || 'Seleccionar' }}
            </span>
          </div>

          <div class="space-y-2 mb-5">
            <button
              v-for="opcion in TRANSICIONES[pedidoParaEstado?.estadoPedido] || []"
              :key="opcion"
              type="button"
              @click="nuevoEstadoSeleccionado = opcion"
              :class="[
                'w-full text-left px-4 py-2.5 rounded-xl text-xs font-bold border transition-all',
                nuevoEstadoSeleccionado === opcion
                  ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white border-transparent shadow-md'
                  : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'
              ]"
            >
              Pasar a: {{ opcion }}
            </button>
          </div>

          <p v-if="nuevoEstadoSeleccionado === 'Cancelado'" class="text-[11px] font-bold text-rose-700 bg-rose-50 border border-rose-200 rounded-xl p-3 mb-4">
            Advertencia: Cancelar este pedido detendrá la producción. El servidor validará que no existan piezas cortadas o confeccionadas[cite: 5].
          </p>

          <div class="flex justify-end gap-2 pt-2 border-t border-slate-100">
            <button @click="cerrarModalEstado" :disabled="isSaving" class="px-4 py-2 text-xs font-bold text-slate-600 hover:bg-slate-100 rounded-xl transition">
              Cancelar
            </button>
            <button
              @click="solicitarConfirmacionEstado"
              :disabled="isSaving || !nuevoEstadoSeleccionado"
              class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition disabled:opacity-50"
            >
              Continuar
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

@keyframes scaleIn {
  from {
    transform: scale(0.96);
    opacity: 0;
  }
  to {
    transform: scale(1);
    opacity: 1;
  }
}

.animate-scale-in {
  animation: scaleIn 0.18s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}
</style>