<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import recetaService from '../services/recetaService'
import articuloService from '../services/articuloService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

// ---------- Estados Principales ----------
const recetas = ref([])
const articulos = ref([])
const unidades = ref([])
const isLoading = ref(true)
const isSaving = ref(false)

// Modales
const showModal = ref(false)
const showDetalleModal = ref(false)
const isEditing = ref(false)
const currentId = ref(null)
const recetaSeleccionada = ref(null)
const recetaOriginal = ref(null)

// Modal de Confirmación Guardar (Resumen Insert / Diff Modificar)
const showConfirmGuardar = ref(false)

// Filtros
const filtroBusqueda = ref('')
const mostrarInactivos = ref(false)

// Formulario de Ficha Técnica
const formReceta = reactive({
  nombreReceta: '',
  idArticuloPrenda: '',
  descripcionReceta: '',
  detalles: []
})
const erroresForm = reactive({})

// Formulario auxiliar para captura de renglón de insumo
const itemInsumoForm = reactive({
  idArticuloInsumo: '',
  idUnidadConsumo: '',
  cantidadNeta: 1,
  porcentajeMerma: 0
})
const editandoInsumoIndex = ref(null)

// ---------- Diálogo de Confirmación Genérico (Pop-in) ----------
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

// ---------- Toasts Notificaciones Flotantes ----------
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

// ---------- Formateadores ----------
const formatMoneda = (val) =>
  new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(val || 0)

// ---------- Computados de Clasificación ----------
const prendasTerminadas = computed(() =>
  articulos.value.filter(a => a.tipoArticulo === 'PrendaTerminada' && a.estadoArticulo)
)

const insumosDisponibles = computed(() =>
  articulos.value.filter(a => ['MateriaPrima', 'Insumo', 'PiezaCorte'].includes(a.tipoArticulo) && a.estadoArticulo)
)

const recetasFiltradas = computed(() => {
  return recetas.value.filter(r => {
    if (!mostrarInactivos.value && !r.estadoReceta) return false
    if (filtroBusqueda.value.trim()) {
      const term = filtroBusqueda.value.toLowerCase().trim()
      const matchNombre = r.nombreReceta?.toLowerCase().includes(term)
      const matchPrenda = r.nombrePrenda?.toLowerCase().includes(term)
      const matchCodigo = r.codigoPrenda?.toLowerCase().includes(term)
      return matchNombre || matchPrenda || matchCodigo
    }
    return true
  })
})

const cantidadBrutaCalculadaItem = computed(() => {
  const neta = Number(itemInsumoForm.cantidadNeta) || 0
  const merma = Number(itemInsumoForm.porcentajeMerma) || 0
  const bruta = neta * (1 + merma / 100)
  return bruta.toFixed(4)
})

const costoTotalEstimado = computed(() => {
  return formReceta.detalles.reduce((acc, det) => {
    const art = articulos.value.find(a => a.idArticulo === det.idArticuloInsumo)
    const costo = art ? (art.costoPromedio || 0) : 0
    return acc + (costo * det.cantidadBruta)
  }, 0)
})

// ---------- KPIs ----------
const kpis = computed(() => {
  const lista = recetas.value
  const total = lista.length
  const activas = lista.filter(r => r.estadoReceta).length
  const prendasConFicha = new Set(lista.filter(r => r.estadoReceta).map(r => r.idArticuloPrenda)).size
  const costoPromedioGral = activas > 0
    ? lista.filter(r => r.estadoReceta).reduce((acc, r) => acc + (r.costoEstimadoTotal || 0), 0) / activas
    : 0

  return { total, activas, prendasConFicha, costoPromedioGral }
})

// ---------- Comparación de Cambios (Diff / Resumen) ----------
const resumenEdicion = computed(() => {
  if (!isEditing.value || !recetaOriginal.value) return []

  const prendaAnt = prendasTerminadas.value.find(p => p.idArticulo === recetaOriginal.value.idArticuloPrenda)
  const prendaNue = prendasTerminadas.value.find(p => p.idArticulo === formReceta.idArticuloPrenda)

  return [
    {
      etiqueta: 'Nombre de Ficha',
      anterior: recetaOriginal.value.nombreReceta,
      nuevo: formReceta.nombreReceta.trim(),
      haCambiado: formReceta.nombreReceta.trim() !== recetaOriginal.value.nombreReceta
    },
    {
      etiqueta: 'Prenda Terminada',
      anterior: prendaAnt ? `${prendaAnt.nombreArticulo} (${prendaAnt.codigoArticulo})` : '—',
      nuevo: prendaNue ? `${prendaNue.nombreArticulo} (${prendaNue.codigoArticulo})` : '—',
      haCambiado: formReceta.idArticuloPrenda !== recetaOriginal.value.idArticuloPrenda
    },
    {
      etiqueta: 'Cantidad de Insumos',
      anterior: `${recetaOriginal.value.detalles.length} materiales`,
      nuevo: `${formReceta.detalles.length} materiales`,
      haCambiado: formReceta.detalles.length !== recetaOriginal.value.detalles.length
    },
    {
      etiqueta: 'Costo Unitario Estimado',
      anterior: formatMoneda(recetaOriginal.value.costoEstimadoTotal),
      nuevo: formatMoneda(costoTotalEstimado.value),
      haCambiado: Math.abs(costoTotalEstimado.value - recetaOriginal.value.costoEstimadoTotal) > 0.01
    }
  ]
})

const cambiosDetectados = computed(() => resumenEdicion.value.filter(c => c.haCambiado))

// ---------- Carga de Datos ----------
const cargarDatos = async () => {
  if (!can('RECETAS', 'consultar')) {
    isLoading.value = false
    return
  }

  isLoading.value = true
  try {
    const [resRecetas, resArticulos, resUnidades] = await Promise.all([
      recetaService.obtenerTodos(),
      articuloService.obtenerTodos(),
      articuloService.obtenerUnidadesMedida()
    ])
    recetas.value = Array.isArray(resRecetas) ? resRecetas : (resRecetas?.data || [])
    articulos.value = Array.isArray(resArticulos) ? resArticulos : (resArticulos?.data || [])
    unidades.value = Array.isArray(resUnidades) ? resUnidades : (resUnidades?.data || [])
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudieron sincronizar las fichas técnicas.')
  } finally {
    isLoading.value = false
  }
}

// ---------- Insumos en la Ficha ----------
const onInsumoChange = () => {
  const insumo = articulos.value.find(a => a.idArticulo === Number(itemInsumoForm.idArticuloInsumo))
  if (insumo) {
    itemInsumoForm.idUnidadConsumo = insumo.idUnidadBaseMedida
  }
}

const agregarOModificarInsumo = () => {
  erroresForm.renglon = ''
  const { idArticuloInsumo, idUnidadConsumo, cantidadNeta, porcentajeMerma } = itemInsumoForm

  if (!idArticuloInsumo) {
    erroresForm.renglon = 'Selecciona un insumo o tela para agregar.'
    return
  }
  if (!idUnidadConsumo) {
    erroresForm.renglon = 'Selecciona la unidad de consumo correspondiente.'
    return
  }
  if (Number(cantidadNeta) <= 0) {
    erroresForm.renglon = 'La cantidad neta debe ser mayor a cero.'
    return
  }
  if (Number(porcentajeMerma) < 0 || Number(porcentajeMerma) > 100) {
    erroresForm.renglon = 'El porcentaje de merma debe estar entre 0% y 100%.'
    return
  }

  const neta = Number(cantidadNeta)
  const merma = Number(porcentajeMerma) || 0
  const bruta = Number((neta * (1 + merma / 100)).toFixed(4))

  if (editandoInsumoIndex.value !== null) {
    formReceta.detalles[editandoInsumoIndex.value] = {
      idArticuloInsumo: Number(idArticuloInsumo),
      idUnidadConsumo: Number(idUnidadConsumo),
      cantidadNeta: neta,
      porcentajeMerma: merma,
      cantidadBruta: bruta
    }
    editandoInsumoIndex.value = null
    mostrarToast('success', 'Componente actualizado en la fórmula.')
  } else {
    const existe = formReceta.detalles.some(d => d.idArticuloInsumo === Number(idArticuloInsumo))
    if (existe) {
      erroresForm.renglon = 'Este insumo ya está incluido en la ficha. Edita su renglón para modificarlo.'
      return
    }

    formReceta.detalles.push({
      idArticuloInsumo: Number(idArticuloInsumo),
      idUnidadConsumo: Number(idUnidadConsumo),
      cantidadNeta: neta,
      porcentajeMerma: merma,
      cantidadBruta: bruta
    })
  }

  itemInsumoForm.cantidadNeta = 1
  itemInsumoForm.porcentajeMerma = 0
}

const seleccionarParaEditarInsumo = (index) => {
  editandoInsumoIndex.value = index
  const linea = formReceta.detalles[index]
  itemInsumoForm.idArticuloInsumo = linea.idArticuloInsumo
  itemInsumoForm.idUnidadConsumo = linea.idUnidadConsumo
  itemInsumoForm.cantidadNeta = linea.cantidadNeta
  itemInsumoForm.porcentajeMerma = linea.porcentajeMerma
}

const quitarInsumo = (index) => {
  if (editandoInsumoIndex.value === index) editandoInsumoIndex.value = null
  formReceta.detalles.splice(index, 1)
}

// ---------- Modales Crear / Editar ----------
const limpiarErrores = () => {
  Object.keys(erroresForm).forEach(k => delete erroresForm[k])
}

const abrirModalCrear = () => {
  if (!can('RECETAS', 'insertar')) return

  if (prendasTerminadas.value.length === 0) {
    mostrarToast('error', 'Debes registrar al menos una prenda terminada activa antes de crear una ficha técnica.')
    return
  }

  isEditing.value = false
  currentId.value = null
  recetaOriginal.value = null
  editandoInsumoIndex.value = null
  limpiarErrores()

  formReceta.nombreReceta = ''
  formReceta.idArticuloPrenda = prendasTerminadas.value[0]?.idArticulo || ''
  formReceta.descripcionReceta = ''
  formReceta.detalles = []

  if (insumosDisponibles.value.length > 0) {
    itemInsumoForm.idArticuloInsumo = insumosDisponibles.value[0].idArticulo
    onInsumoChange()
  }

  showModal.value = true
}

const abrirModalEditar = async (item) => {
  if (!can('RECETAS', 'modificar')) return
  isEditing.value = true
  currentId.value = item.idReceta
  editandoInsumoIndex.value = null
  limpiarErrores()

  try {
    const detalleData = await recetaService.obtenerDetalle(item.idReceta)

    recetaOriginal.value = {
      nombreReceta: detalleData.nombreReceta,
      idArticuloPrenda: detalleData.idArticuloPrenda,
      descripcionReceta: detalleData.descripcionReceta || '',
      costoEstimadoTotal: Number(detalleData.costoEstimadoTotal || 0),
      detalles: detalleData.detalles.map(d => ({
        idArticuloInsumo: d.idArticuloInsumo,
        idUnidadConsumo: d.idUnidadConsumo,
        cantidadNeta: Number(d.cantidadNeta),
        porcentajeMerma: Number(d.porcentajeMerma),
        cantidadBruta: Number(d.cantidadBruta)
      }))
    }

    formReceta.nombreReceta = detalleData.nombreReceta
    formReceta.idArticuloPrenda = detalleData.idArticuloPrenda
    formReceta.descripcionReceta = detalleData.descripcionReceta || ''
    formReceta.detalles = detalleData.detalles.map(d => ({
      idArticuloInsumo: d.idArticuloInsumo,
      idUnidadConsumo: d.idUnidadConsumo,
      cantidadNeta: Number(d.cantidadNeta),
      porcentajeMerma: Number(d.porcentajeMerma),
      cantidadBruta: Number(d.cantidadBruta)
    }))

    if (insumosDisponibles.value.length > 0) {
      itemInsumoForm.idArticuloInsumo = insumosDisponibles.value[0].idArticulo
      onInsumoChange()
    }

    showModal.value = true
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo cargar la ficha técnica.')
  }
}

const verDetalle = async (item) => {
  recetaSeleccionada.value = null
  try {
    recetaSeleccionada.value = await recetaService.obtenerDetalle(item.idReceta)
    showDetalleModal.value = true
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo obtener el desglose de la ficha.')
  }
}

// ---------- Validaciones y Envío ----------
const validarFormulario = () => {
  limpiarErrores()
  const nom = formReceta.nombreReceta?.trim() || ''
  const desc = formReceta.descripcionReceta?.trim() || ''

  if (!nom) {
    erroresForm.nombreReceta = 'El nombre de la ficha técnica es obligatorio.'
  } else if (!TextRules.esNombreValido(nom, 3, 100)) {
    erroresForm.nombreReceta = 'El nombre debe contener entre 3 y 100 caracteres válidos.'
  }

  if (desc && !TextRules.esDescripcionValida(desc, 500)) {
    erroresForm.descripcionReceta = 'La descripción contiene símbolos no admitidos o excede 500 caracteres.'
  }

  if (!formReceta.idArticuloPrenda) {
    erroresForm.idArticuloPrenda = 'Debes seleccionar la prenda terminada que fabricará esta receta.'
  }

  if (formReceta.detalles.length === 0) {
    erroresForm.general = 'Debes asignar al menos una tela o insumo a la ficha técnica (BOM).'
  }

  return Object.keys(erroresForm).length === 0
}

const solicitarGuardar = () => {
  if (!validarFormulario()) return

  if (isEditing.value) {
    if (cambiosDetectados.value.length === 0) {
      mostrarToast('error', 'No realizaste ninguna modificación en la ficha técnica.')
      return
    }
    showConfirmGuardar.value = true
  } else {
    showConfirmGuardar.value = true
  }
}

const ejecutarGuardado = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const payload = {
    nombreReceta: formReceta.nombreReceta.trim(),
    idArticuloPrenda: Number(formReceta.idArticuloPrenda),
    descripcionReceta: formReceta.descripcionReceta?.trim() || null,
    detalles: formReceta.detalles.map(d => ({
      idArticuloInsumo: d.idArticuloInsumo,
      idUnidadConsumo: d.idUnidadConsumo,
      cantidadNeta: d.cantidadNeta,
      porcentajeMerma: d.porcentajeMerma
    }))
  }

  try {
    if (isEditing.value) {
      await recetaService.actualizar(currentId.value, payload)
      mostrarToast('success', `Ficha técnica "${payload.nombreReceta}" actualizada exitosamente.`)
    } else {
      await recetaService.crear(payload)
      mostrarToast('success', `Ficha técnica "${payload.nombreReceta}" registrada correctamente.`)
    }
    showConfirmGuardar.value = false
    showModal.value = false
    await cargarDatos()
  } catch (err) {
    showConfirmGuardar.value = false
    const msg = err?.response?.data?.message || 'Error al procesar la ficha técnica.'
    erroresForm.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

// ---------- Toggle Estado ----------
const solicitarToggleEstado = async (item) => {
  if (!can('RECETAS', 'eliminar')) return
  const accion = item.estadoReceta ? 'inhabilitar' : 'activar'

  const confirmado = await pedirConfirmacion({
    variant: item.estadoReceta ? 'danger' : 'primary',
    title: `¿Confirmar cambio de estado?`,
    message: `¿Deseas ${accion} la ficha técnica "${item.nombreReceta}"?`,
    detalles: [
      `Prenda: ${item.nombrePrenda} (${item.codigoPrenda})`,
      `Componentes: ${item.totalInsumos} insumos`,
      `Costo Estimado: ${formatMoneda(item.costoEstimadoTotal)}`
    ],
    confirmText: `Sí, ${accion}`,
    cancelText: 'Cancelar'
  })

  if (!confirmado) return

  try {
    const res = await recetaService.cambiarEstado(item.idReceta)
    mostrarToast('success', res?.message || `Ficha técnica ${accion}da exitosamente.`)
    await cargarDatos()
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo cambiar el estado de la ficha.')
  }
}

// Control Teclado (Escape)
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) resolverConfirmacion(false)
  else if (showConfirmGuardar.value) showConfirmGuardar.value = false
  else if (showDetalleModal.value) showDetalleModal.value = false
  else if (showModal.value) showModal.value = false
}

onMounted(() => {
  window.addEventListener('keydown', onKeydown)
  cargarDatos()
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <MainLayout>
    <!-- Modal Confirmación de Modificaciones / Resumen Inserción -->
    <Transition name="fade">
      <div v-if="showConfirmGuardar" class="fixed inset-0 z-[65] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4" role="dialog" aria-modal="true">
        <div class="bg-white rounded-3xl p-6 sm:p-7 shadow-2xl w-full max-w-lg border border-slate-100 text-center animate-scale-in">
          <div class="w-16 h-16 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center mx-auto mb-4 border border-purple-100 shadow-sm">
            <svg class="w-8 h-8 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4" />
            </svg>
          </div>

          <h3 class="text-lg font-black text-slate-800 tracking-tight">
            {{ isEditing ? '¿Guardar los cambios en la Ficha?' : '¿Confirmar registro de Ficha Técnica?' }}
          </h3>
          <p class="text-xs text-slate-500 font-medium mt-1 mb-5">
            {{ isEditing ? 'Compara las especificaciones modificadas antes de actualizar la fórmula.' : 'Revisa el resumen de materiales y costos antes de registrar la fórmula oficial.' }}
          </p>

          <!-- Si es Edición: Diff Visual -->
          <div v-if="isEditing" class="bg-slate-50/80 border border-slate-200/70 rounded-2xl p-4 text-left space-y-3 mb-6 max-h-60 overflow-y-auto">
            <div
              v-for="item in resumenEdicion"
              :key="item.etiqueta"
              class="text-xs pb-2.5 last:pb-0 border-b border-slate-200/60 last:border-0"
            >
              <div class="flex items-center justify-between">
                <span class="font-bold text-slate-500 text-[11px] uppercase tracking-wider">{{ item.etiqueta }}</span>
                <span
                  v-if="item.haCambiado"
                  class="text-[10px] font-bold text-purple-700 bg-purple-100 px-2 py-0.5 rounded-full border border-purple-200"
                >
                  Modificado
                </span>
                <span v-else class="text-[10px] font-semibold text-slate-400">Sin cambio</span>
              </div>

              <div v-if="item.haCambiado" class="mt-1 space-y-0.5">
                <div class="text-[11px] text-slate-400 line-through">
                  <span class="font-semibold text-slate-400">Anterior:</span> {{ item.anterior }}
                </div>
                <div class="text-xs font-bold text-slate-800">
                  <span class="text-purple-600 font-bold">Nuevo:</span> {{ item.nuevo }}
                </div>
              </div>

              <div v-else class="mt-0.5 text-xs font-semibold text-slate-700">
                {{ item.nuevo }}
              </div>
            </div>
          </div>

          <!-- Si es Inserción: Resumen Estructurado -->
          <div v-else class="bg-slate-50/80 border border-slate-200/70 rounded-2xl p-4 text-left space-y-2.5 mb-6 max-h-60 overflow-y-auto">
            <div class="flex justify-between items-center text-xs pb-2 border-b border-slate-200/60">
              <span class="font-bold text-slate-500 uppercase">Ficha / Receta:</span>
              <span class="font-bold text-slate-800">{{ formReceta.nombreReceta }}</span>
            </div>
            <div class="flex justify-between items-center text-xs pb-2 border-b border-slate-200/60">
              <span class="font-bold text-slate-500 uppercase">Prenda Destino:</span>
              <span class="font-bold text-purple-700">
                {{ prendasTerminadas.find(p => p.idArticulo === formReceta.idArticuloPrenda)?.nombreArticulo }}
              </span>
            </div>
            <div class="flex justify-between items-center text-xs pb-2 border-b border-slate-200/60">
              <span class="font-bold text-slate-500 uppercase">Total Componentes:</span>
              <span class="font-bold text-slate-800 font-mono">{{ formReceta.detalles.length }} insumos</span>
            </div>
            <div class="flex justify-between items-center text-xs pt-0.5">
              <span class="font-bold text-slate-500 uppercase">Costo Unit. Estimado:</span>
              <span class="font-mono font-black text-emerald-600 text-sm">{{ formatMoneda(costoTotalEstimado) }}</span>
            </div>
          </div>

          <div class="flex items-center gap-3">
            <button
              type="button"
              @click="showConfirmGuardar = false"
              :disabled="isSaving"
              class="flex-1 py-2.5 px-4 text-xs font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors disabled:opacity-50"
            >
              Volver a revisar
            </button>
            <button
              type="button"
              @click="ejecutarGuardado"
              :disabled="isSaving"
              class="flex-1 py-2.5 px-4 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg transition-all disabled:opacity-60"
            >
              {{ isSaving ? 'Guardando...' : (isEditing ? 'Sí, confirmar cambios' : 'Sí, registrar ficha') }}
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
        <div class="pop-card bg-white rounded-3xl shadow-2xl w-full max-w-sm overflow-hidden">
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

    <!-- Restricción de Permiso -->
    <div v-if="!can('RECETAS', 'consultar')" class="bg-white rounded-3xl p-14 text-center shadow-md border border-slate-100">
      <div class="w-16 h-16 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4 ring-8 ring-rose-50/60">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-base font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-sm text-slate-500 mt-1.5 max-w-sm mx-auto">No cuentas con autorización para consultar las fichas técnicas (BOM).</p>
    </div>

    <!-- Contenedor Principal -->
    <div v-else class="space-y-6">
      <!-- Encabezado con Botón Principal -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Fichas Técnicas de Confección (BOM)</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de explosión de materiales, consumo neto, merma estimada y cantidad bruta</p>
        </div>
        <button
          v-if="can('RECETAS', 'insertar')"
          @click="abrirModalCrear"
          class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:scale-[1.01] active:scale-[0.98] transition-all whitespace-nowrap"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" /></svg>
          Nueva Ficha Técnica
        </button>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Total Fichas</p>
            <p class="text-2xl font-black text-slate-900 mt-1 font-mono">{{ kpis.total }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center border border-purple-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Fichas Activas</p>
            <p class="text-2xl font-black text-emerald-600 mt-1 font-mono">{{ kpis.activas }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center border border-emerald-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Prendas con Fórmula</p>
            <p class="text-2xl font-black text-indigo-600 mt-1 font-mono">{{ kpis.prendasConFicha }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center border border-indigo-100 shadow-sm">
            <span class="font-mono font-black text-xs">BOM</span>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Costo Promedio / Pza</p>
            <p class="text-xl font-black text-purple-700 mt-1 font-mono">{{ formatMoneda(kpis.costoPromedioGral) }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center border border-purple-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          </div>
        </div>
      </div>

      <!-- Barra de Filtros -->
      <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 flex flex-col md:flex-row md:items-center gap-3">
        <div class="relative flex-1">
          <input
            v-model="filtroBusqueda"
            type="text"
            placeholder="Buscar por nombre de ficha, prenda terminada o código de artículo..."
            class="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
          />
          <svg class="w-4 h-4 text-slate-400 absolute left-3.5 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>

        <label class="flex items-center gap-2 text-xs font-bold text-slate-600 bg-slate-50 px-3.5 py-2.5 rounded-xl border border-slate-200 cursor-pointer select-none">
          <input type="checkbox" v-model="mostrarInactivos" class="rounded text-purple-600 focus:ring-purple-500" />
          <span>Mostrar inactivas</span>
        </label>
      </div>

      <!-- Tabla de Datos Refinada -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-100">
          <h3 class="text-sm font-black text-slate-800">Fichas Técnicas Homologadas</h3>
          <span v-if="!isLoading" class="text-xs font-semibold text-slate-500">
            {{ recetasFiltradas.length }} {{ recetasFiltradas.length === 1 ? 'registro' : 'registros' }}
          </span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[1000px] text-left border-collapse">
            <thead>
              <tr class="bg-slate-50/70 border-b border-slate-100 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                <th class="py-3.5 px-6 whitespace-nowrap min-w-[200px]">Ficha / Receta</th>
                <th class="py-3.5 px-4 whitespace-nowrap min-w-[170px]">Prenda Terminada</th>
                <th class="py-3.5 px-4 whitespace-nowrap min-w-[160px]">Variantes</th>
                <th class="py-3.5 px-4 text-center whitespace-nowrap min-w-[140px]">Insumos</th>
                <th class="py-3.5 px-4 text-right whitespace-nowrap min-w-[120px]">Costo Estimado</th>
                <th class="py-3.5 px-4 text-center whitespace-nowrap min-w-[100px]">Estado</th>
                <th class="py-3.5 px-6 text-right whitespace-nowrap min-w-[180px]">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-if="isLoading">
                <td colspan="7" class="py-14 text-center text-slate-400 font-semibold">
                  Cargando fichas técnicas...
                </td>
              </tr>

              <tr
                v-for="r in recetasFiltradas"
                :key="r.idReceta"
                :class="['hover:bg-purple-50/20 transition-colors', !r.estadoReceta ? 'opacity-60 bg-slate-50/40' : '']"
              >
                <!-- Ficha / Receta -->
                <td class="py-3.5 px-6 whitespace-nowrap">
                  <span class="block text-slate-900 font-bold text-[13px] leading-tight">{{ r.nombreReceta }}</span>
                  <span class="inline-block mt-1 px-1.5 py-0.5 rounded-md bg-purple-50 text-[10px] text-purple-700 font-mono font-bold border border-purple-200">
                    REC-#{{ r.idReceta }}
                  </span>
                </td>

                <!-- Prenda Terminada -->
                <td class="py-3.5 px-4 whitespace-nowrap">
                  <span class="block font-bold text-slate-900">{{ r.nombrePrenda }}</span>
                  <span class="text-[10px] text-slate-400 font-mono">{{ r.codigoPrenda }}</span>
                </td>

                <!-- Variantes (Rediseñadas con badges fluidos) -->
                <td class="py-3.5 px-4 whitespace-nowrap">
                  <div class="flex items-center gap-1.5 flex-nowrap">
                    <!-- Badge de Talla -->
                    <span
                      v-if="r.talla"
                      class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-indigo-50 border border-indigo-200/80 text-indigo-700 text-[11px] font-bold whitespace-nowrap shadow-2xs"
                    >
                      <svg class="w-3 h-3 text-indigo-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
                      </svg>
                      <span>Talla {{ r.talla }}</span>
                    </span>

                    <!-- Badge de Color -->
                    <span
                      v-if="r.color"
                      class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-purple-50 border border-purple-200/80 text-purple-700 text-[11px] font-bold whitespace-nowrap shadow-2xs"
                    >
                      <span class="w-1.5 h-1.5 rounded-full bg-purple-500 shrink-0"></span>
                      <span>{{ r.color }}</span>
                    </span>

                    <!-- Sin variantes -->
                    <span v-if="!r.talla && !r.color" class="text-slate-300 font-mono text-xs">—</span>
                  </div>
                </td>

                <!-- Insumos (Rediseñados con píldora uniforme horizontal) -->
                <td class="py-3.5 px-4 text-center whitespace-nowrap">
                  <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg text-xs font-bold bg-indigo-50/80 border border-indigo-200 text-indigo-700 whitespace-nowrap shadow-2xs">
                    <svg class="w-3.5 h-3.5 text-indigo-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                    </svg>
                    <span>{{ r.totalInsumos }} {{ r.totalInsumos === 1 ? 'insumo' : 'insumos' }}</span>
                  </span>
                </td>

                <!-- Costo Estimado -->
                <td class="py-3.5 px-4 text-right font-mono font-black text-slate-800 whitespace-nowrap">
                  {{ formatMoneda(r.costoEstimadoTotal) }}
                </td>

                <!-- Estado -->
                <td class="py-3.5 px-4 text-center whitespace-nowrap">
                  <span :class="[
                    'px-2.5 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-2xs',
                    r.estadoReceta ? 'bg-emerald-50 text-emerald-700 border-emerald-300' : 'bg-rose-50 text-rose-700 border-rose-300'
                  ]">
                    {{ r.estadoReceta ? 'Activa' : 'Inactiva' }}
                  </span>
                </td>

                <!-- Acciones -->
                <td class="py-3.5 px-6 text-right whitespace-nowrap">
                  <div class="flex items-center justify-end gap-1.5">
                    <button
                      @click="verDetalle(r)"
                      class="px-2.5 py-1.5 bg-purple-50 hover:bg-purple-600 text-purple-700 hover:text-white rounded-lg text-xs font-bold transition-all shadow-xs border border-purple-200"
                    >
                      Fórmula
                    </button>
                    <button
                      v-if="can('RECETAS', 'modificar')"
                      @click="abrirModalEditar(r)"
                      class="px-2.5 py-1.5 bg-slate-100 hover:bg-purple-600 text-slate-700 hover:text-white rounded-lg text-xs font-bold transition-all shadow-xs border border-slate-200"
                    >
                      Editar
                    </button>
                    <button
                      v-if="can('RECETAS', 'eliminar')"
                      @click="solicitarToggleEstado(r)"
                      :class="[
                        'px-2.5 py-1.5 rounded-lg text-xs font-bold transition-all border shadow-xs',
                        r.estadoReceta
                          ? 'bg-rose-50 hover:bg-rose-600 text-rose-700 hover:text-white border-rose-200'
                          : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-700 hover:text-white border-emerald-200'
                      ]"
                    >
                      {{ r.estadoReceta ? 'Inhabilitar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>

              <tr v-if="recetasFiltradas.length === 0 && !isLoading">
                <td colspan="7" class="py-14 text-center">
                  <div class="w-14 h-14 bg-purple-50 text-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-3 border border-purple-100">
                    <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" /></svg>
                  </div>
                  <p class="text-sm font-black text-slate-800">No se encontraron fichas técnicas registradas</p>
                  <p class="text-xs text-slate-500 font-medium mt-1">Crea una nueva fórmula para calcular consumo de materiales.</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Formulario Creación / Edición -->
    <Transition name="fade">
      <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-3xl shadow-2xl w-full max-w-2xl border border-slate-100 max-h-[92vh] flex flex-col overflow-hidden animate-scale-in">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <!-- Cabecera -->
          <div class="px-7 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">
                {{ isEditing ? 'Editar Ficha Técnica (BOM)' : 'Nueva Ficha Técnica de Confección' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Define los consumos por prenda, mermas de corte y tolerancias operativas</p>
            </div>
            <button @click="showModal = false" :disabled="isSaving" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <form @submit.prevent="solicitarGuardar" class="flex flex-col min-h-0 flex-1">
            <div class="px-7 py-5 space-y-4 overflow-y-auto">
              <div v-if="erroresForm.general" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-bold rounded-xl px-4 py-3">
                {{ erroresForm.general }}
              </div>

              <!-- Encabezado de la Ficha -->
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
                <div>
                  <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                    Nombre de la Receta / Ficha <span class="text-rose-500">*</span>
                  </label>
                  <input
                    v-model="formReceta.nombreReceta"
                    @keypress="allowOnly.nombreInput($event)"
                    required
                    maxlength="100"
                    type="text"
                    placeholder="Ej: Playera Polo Clásica Piqué"
                    class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                  />
                  <p v-if="erroresForm.nombreReceta" class="text-rose-600 text-[11px] font-bold mt-1">
                    {{ erroresForm.nombreReceta }}
                  </p>
                </div>

                <div>
                  <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                    Prenda Terminada Destino <span class="text-rose-500">*</span>
                  </label>
                  <select
                    v-model="formReceta.idArticuloPrenda"
                    required
                    class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                  >
                    <option v-for="pt in prendasTerminadas" :key="pt.idArticulo" :value="pt.idArticulo">
                      {{ pt.nombreArticulo }} ({{ pt.codigoArticulo }})
                    </option>
                  </select>
                  <p v-if="erroresForm.idArticuloPrenda" class="text-rose-600 text-[11px] font-bold mt-1">
                    {{ erroresForm.idArticuloPrenda }}
                  </p>
                </div>
              </div>

              <!-- Descripción -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Descripción / Instrucciones Técnicas
                </label>
                <textarea
                  v-model="formReceta.descripcionReceta"
                  @keypress="allowOnly.descripcionInput($event)"
                  rows="2"
                  maxlength="500"
                  placeholder="Instrucciones de patronaje, sentido del hilo, tolerancias de costura..."
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                ></textarea>
                <p v-if="erroresForm.descripcionReceta" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.descripcionReceta }}
                </p>
              </div>

              <!-- Formulario de Renglón: Componentes / Materiales -->
              <div :class="['p-4 rounded-2xl border transition-all', editandoInsumoIndex !== null ? 'bg-purple-50/50 border-purple-300 ring-2 ring-purple-400/20' : 'bg-slate-50/80 border-slate-200']">
                <div class="flex items-center justify-between mb-2.5">
                  <h4 class="text-xs font-black uppercase tracking-wider text-slate-700">
                    {{ editandoInsumoIndex !== null ? `✏️ Modificando Renglón #${editandoInsumoIndex + 1}` : 'Agregar Insumo o Tela a la Ficha' }}
                  </h4>
                  <span class="text-[11px] font-mono font-bold text-purple-700">
                    Bruta Requerida: {{ cantidadBrutaCalculadaItem }}
                  </span>
                </div>
                <p v-if="erroresForm.renglon" class="text-rose-600 text-[11px] font-bold mb-2">{{ erroresForm.renglon }}</p>

                <div class="grid grid-cols-1 sm:grid-cols-12 gap-2.5 items-end">
                  <div class="sm:col-span-4">
                    <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Insumo / Tela</label>
                    <select
                      v-model="itemInsumoForm.idArticuloInsumo"
                      @change="onInsumoChange"
                      class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-800 outline-none focus:border-purple-500"
                    >
                      <option v-for="ins in insumosDisponibles" :key="ins.idArticulo" :value="ins.idArticulo">
                        {{ ins.nombreArticulo }} ({{ ins.codigoArticulo }})
                      </option>
                    </select>
                  </div>

                  <div class="sm:col-span-3">
                    <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">U. Consumo</label>
                    <select
                      v-model="itemInsumoForm.idUnidadConsumo"
                      class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-800 outline-none focus:border-purple-500"
                    >
                      <option v-for="u in unidades" :key="u.idUnidadMedida" :value="u.idUnidadMedida">
                        {{ u.nombreMedida }} ({{ u.codigoMedida }})
                      </option>
                    </select>
                  </div>

                  <div class="sm:col-span-2">
                    <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Cant. Neta</label>
                    <input
                      v-model.number="itemInsumoForm.cantidadNeta"
                      type="number"
                      step="0.0001"
                      min="0.0001"
                      placeholder="1.0"
                      class="w-full px-2.5 py-2 bg-white border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 outline-none focus:border-purple-500"
                    />
                  </div>

                  <div class="sm:col-span-2">
                    <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">% Merma</label>
                    <input
                      v-model.number="itemInsumoForm.porcentajeMerma"
                      type="number"
                      step="0.1"
                      min="0"
                      max="100"
                      placeholder="0.0"
                      class="w-full px-2.5 py-2 bg-white border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 outline-none focus:border-purple-500"
                    />
                  </div>

                  <div class="sm:col-span-1">
                    <button
                      type="button"
                      @click="agregarOModificarInsumo"
                      class="w-full py-2 bg-gradient-to-tl from-purple-700 to-pink-500 hover:opacity-95 text-white font-bold text-xs rounded-xl shadow-md transition"
                    >
                      {{ editandoInsumoIndex !== null ? '✓' : '+' }}
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
                      <th class="py-2.5 px-3 text-right">Cant. Neta</th>
                      <th class="py-2.5 px-3 text-right">% Merma</th>
                      <th class="py-2.5 px-3 text-right">Cant. Bruta</th>
                      <th class="py-2.5 px-3 text-right">Subtotal Est.</th>
                      <th class="py-2.5 px-3 text-center">Quitar</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-100 text-xs">
                    <tr
                      v-for="(det, index) in formReceta.detalles"
                      :key="det.idArticuloInsumo"
                      @click="seleccionarParaEditarInsumo(index)"
                      :class="[
                        'cursor-pointer transition-colors',
                        editandoInsumoIndex === index ? 'bg-purple-100/70 border-l-4 border-l-purple-600 font-bold' : 'hover:bg-purple-50/30'
                      ]"
                    >
                      <td class="py-2.5 px-3.5 font-bold text-slate-800">
                        {{ articulos.find(a => a.idArticulo === det.idArticuloInsumo)?.nombreArticulo }}
                        <span class="text-[10px] text-purple-700 font-mono">
                          ({{ articulos.find(a => a.idArticulo === det.idArticuloInsumo)?.codigoArticulo }})
                        </span>
                      </td>
                      <td class="py-2.5 px-3 text-right font-mono">
                        {{ det.cantidadNeta }} {{ unidades.find(u => u.idUnidadMedida === det.idUnidadConsumo)?.codigoMedida }}
                      </td>
                      <td class="py-2.5 px-3 text-right font-mono text-amber-600 font-bold">
                        {{ det.porcentajeMerma }}%
                      </td>
                      <td class="py-2.5 px-3 text-right font-mono font-bold text-slate-900">
                        {{ det.cantidadBruta }} {{ unidades.find(u => u.idUnidadMedida === det.idUnidadConsumo)?.codigoMedida }}
                      </td>
                      <td class="py-2.5 px-3 text-right font-mono text-purple-700 font-bold">
                        {{ formatMoneda((articulos.find(a => a.idArticulo === det.idArticuloInsumo)?.costoPromedio || 0) * det.cantidadBruta) }}
                      </td>
                      <td class="py-2.5 px-3 text-center" @click.stop>
                        <button
                          type="button"
                          @click="quitarInsumo(index)"
                          class="p-1 rounded-lg text-rose-500 hover:bg-rose-600 hover:text-white transition font-bold"
                        >
                          ✕
                        </button>
                      </td>
                    </tr>
                    <tr v-if="formReceta.detalles.length === 0">
                      <td colspan="6" class="py-8 text-center text-slate-400 text-xs italic font-semibold">
                        No se han asignado insumos a esta ficha. Selecciona un material arriba y presiona +.
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <!-- Costo Unitario Total Estimado -->
              <div class="flex items-center justify-between p-3.5 bg-purple-50/70 border border-purple-200 rounded-2xl">
                <span class="text-xs font-black text-slate-800 uppercase tracking-wide">Costo Unitario Total con Merma:</span>
                <span class="text-base font-mono font-black text-purple-700">{{ formatMoneda(costoTotalEstimado) }}</span>
              </div>
            </div>

            <!-- Footer Modal -->
            <div class="px-7 py-4 border-t border-slate-100 flex items-center justify-end gap-2.5 bg-slate-50/50">
              <button
                type="button"
                @click="showModal = false"
                :disabled="isSaving"
                class="px-4 py-2.5 text-xs font-bold text-slate-600 hover:bg-slate-200/70 rounded-xl transition-colors disabled:opacity-50"
              >
                Cancelar
              </button>
              <button
                type="submit"
                :disabled="isSaving || formReceta.detalles.length === 0"
                class="px-5 py-2.5 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg transition-all disabled:opacity-60"
              >
                {{ isSaving ? 'Guardando...' : (isEditing ? 'Revisar y Guardar' : 'Revisar y Registrar') }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Transition>

    <!-- Modal Detalle Fórmula (Inspección) -->
    <Transition name="fade">
      <div v-if="showDetalleModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-3xl p-6 shadow-2xl w-full max-w-2xl border border-slate-100 max-h-[85vh] flex flex-col overflow-hidden animate-scale-in">
          <div class="flex items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <h3 class="text-base font-black text-slate-800">Ficha: {{ recetaSeleccionada?.nombreReceta }}</h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">
                Prenda: {{ recetaSeleccionada?.nombreArticulo }} ({{ recetaSeleccionada?.codigoArticulo }})
              </p>
            </div>
            <button @click="showDetalleModal = false" class="p-1 rounded-lg text-slate-400 hover:text-slate-700">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <div class="my-4 overflow-y-auto px-1 flex-1">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="border-b border-slate-100 bg-slate-50/70 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                  <th class="py-2.5 px-3">Componente / Tela</th>
                  <th class="py-2.5 px-3 text-right">Cant. Neta</th>
                  <th class="py-2.5 px-3 text-right">% Merma</th>
                  <th class="py-2.5 px-3 text-right">Cant. Bruta</th>
                  <th class="py-2.5 px-3 text-right">Subtotal</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-for="d in recetaSeleccionada?.detalles" :key="d.idArticuloInsumo" class="hover:bg-purple-50/20">
                  <td class="py-2.5 px-3 font-bold text-slate-800">
                    <span class="block">{{ d.nombreInsumo }}</span>
                    <span class="text-[10px] text-purple-700 font-mono">{{ d.codigoInsumo }}</span>
                  </td>
                  <td class="py-2.5 px-3 text-right font-mono text-slate-600">
                    {{ Number(d.cantidadNeta).toFixed(4) }} {{ d.codigoMedida }}
                  </td>
                  <td class="py-2.5 px-3 text-right font-mono font-bold text-amber-600">
                    {{ Number(d.porcentajeMerma).toFixed(2) }}%
                  </td>
                  <td class="py-2.5 px-3 text-right font-mono font-black text-slate-900">
                    {{ Number(d.cantidadBruta).toFixed(4) }} {{ d.codigoMedida }}
                  </td>
                  <td class="py-2.5 px-3 text-right font-mono font-black text-purple-700">
                    {{ formatMoneda(d.subtotal) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="px-2 py-3 bg-purple-50/70 border border-purple-200 rounded-2xl flex items-center justify-between mb-4">
            <span class="text-xs font-black text-slate-800 uppercase">Costo Total Estimado por Prenda:</span>
            <span class="text-base font-mono font-black text-purple-700">{{ formatMoneda(recetaSeleccionada?.costoEstimadoTotal) }}</span>
          </div>

          <div class="flex justify-end">
            <button
              @click="showDetalleModal = false"
              class="px-5 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition"
            >
              Cerrar
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

@keyframes scaleIn {
  from { transform: scale(0.96); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
}
.animate-scale-in {
  animation: scaleIn 0.18s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}
</style>