<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import unidadService from '../services/unidadService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

// ---------- Variantes del Diálogo Pop-in ----------
const CONFIRM_VARIANTS = {
  danger: {
    iconWrap: 'bg-rose-100 text-rose-600 ring-8 ring-rose-50',
    button: 'bg-gradient-to-tl from-rose-600 to-red-500 shadow-rose-500/30 hover:shadow-lg',
    panel: 'bg-rose-50/60 border-rose-100',
    bar: 'from-rose-500 to-red-400',
    icon: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z'
  },
  warning: {
    iconWrap: 'bg-amber-100 text-amber-600 ring-8 ring-amber-50',
    button: 'bg-gradient-to-tl from-amber-500 to-orange-500 shadow-amber-500/30 hover:shadow-lg',
    panel: 'bg-amber-50/60 border-amber-100',
    bar: 'from-amber-400 to-orange-500',
    icon: 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
  },
  primary: {
    iconWrap: 'bg-purple-100 text-purple-600 ring-8 ring-purple-50',
    button: 'bg-gradient-to-tl from-purple-700 to-pink-500 shadow-purple-500/30 hover:shadow-lg',
    panel: 'bg-purple-50/60 border-purple-100',
    bar: 'from-purple-600 to-pink-500',
    icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z'
  }
}

// ---------- Estados Principales ----------
const activeTab = ref('unidades') // 'unidades' | 'conversiones'
const unidades = ref([])
const conversiones = ref([])
const isLoading = ref(true)
const isSaving = ref(false)

// Buscadores reactivos
const filtroUnidad = ref('')
const filtroTipo = ref('')
const filtroConversion = ref('')

// Modales Formularios
const showModalUnidad = ref(false)
const isEditingUnidad = ref(false)
const currentUnidadId = ref(null)
const unidadOriginal = ref(null)

const showModalConversion = ref(false)
const isEditingConversion = ref(false)
const conversionOriginal = ref(null)

// Modal de Confirmación Guardar (Diff / Resumen Estructurado)
const showConfirmModal = ref(false)
const confirmModalConfig = reactive({
  title: '',
  subtitle: '',
  type: 'unidad_insert', // 'unidad_insert' | 'unidad_diff' | 'conversion_resumen'
  detalles: [],
  onConfirm: null
})

// Diálogo Pop-in genérico (para eliminaciones)
const confirmDialog = reactive({
  visible: false,
  variant: 'primary',
  title: '',
  message: '',
  detalles: [],
  confirmText: 'Confirmar',
  cancelText: 'Cancelar'
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
      cancelText: 'Cancelar',
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
    title: 'Aviso del Sistema',
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

// ---------- Formularios Reactivos ----------
const formUnidad = reactive({
  codigoMedida: '',
  nombreMedida: '',
  tipoMedida: 'Longitud'
})
const erroresUnidad = reactive({})

const formConversion = reactive({
  idUnidadOrigen: '',
  idUnidadDestino: '',
  factorConversion: 1
})
const erroresConversion = reactive({})

// ---------- Estilos por Dimensión ----------
const DIMENSION_CONFIG = {
  Longitud: {
    badge: 'bg-purple-50 text-purple-700 border-purple-300',
    dot: 'bg-purple-500'
  },
  Masa: {
    badge: 'bg-sky-50 text-sky-700 border-sky-300',
    dot: 'bg-sky-500'
  },
  Unidad: {
    badge: 'bg-emerald-50 text-emerald-700 border-emerald-300',
    dot: 'bg-emerald-500'
  },
  Volumen: {
    badge: 'bg-amber-50 text-amber-700 border-amber-300',
    dot: 'bg-amber-500'
  }
}

const getTipoBadgeClass = (tipo) => {
  return DIMENSION_CONFIG[tipo]?.badge || 'bg-slate-50 text-slate-700 border-slate-300'
}

const getTipoDotClass = (tipo) => {
  return DIMENSION_CONFIG[tipo]?.dot || 'bg-slate-400'
}

// ---------- KPIs Analíticos ----------
const kpis = computed(() => {
  const uList = unidades.value
  const cList = conversiones.value
  const dimensiones = new Set(uList.map(u => u.tipoMedida)).size
  const articulosAsociados = uList.reduce((acc, u) => acc + (u.totalArticulosAsociados || 0), 0)
  return {
    totalUnidades: uList.length,
    totalConversiones: cList.length,
    dimensiones,
    articulosAsociados
  }
})

// ---------- Filtros Computados ----------
const unidadesFiltradas = computed(() => {
  return unidades.value.filter(u => {
    if (filtroTipo.value && u.tipoMedida !== filtroTipo.value) return false
    if (filtroUnidad.value.trim()) {
      const t = filtroUnidad.value.toLowerCase().trim()
      return (
        u.codigoMedida.toLowerCase().includes(t) ||
        u.nombreMedida.toLowerCase().includes(t) ||
        u.tipoMedida.toLowerCase().includes(t)
      )
    }
    return true
  })
})

const conversionesFiltradas = computed(() => {
  return conversiones.value.filter(c => {
    if (filtroConversion.value.trim()) {
      const t = filtroConversion.value.toLowerCase().trim()
      return (
        c.codigoOrigen.toLowerCase().includes(t) ||
        c.nombreOrigen.toLowerCase().includes(t) ||
        c.codigoDestino.toLowerCase().includes(t) ||
        c.nombreDestino.toLowerCase().includes(t) ||
        c.tipoMedida.toLowerCase().includes(t)
      )
    }
    return true
  })
})

// Unidades disponibles para destino (misma dimensión física, no idéntica)
const unidadesDestinoCompatibles = computed(() => {
  if (!formConversion.idUnidadOrigen) return []
  const origen = unidades.value.find(u => u.idUnidadMedida === Number(formConversion.idUnidadOrigen))
  if (!origen) return []
  return unidades.value.filter(u => u.tipoMedida === origen.tipoMedida && u.idUnidadMedida !== origen.idUnidadMedida)
})

// Cálculo de equivalencias recíprocas en tiempo real para el formulario
const unidadOrigenObj = computed(() =>
  unidades.value.find(u => u.idUnidadMedida === Number(formConversion.idUnidadOrigen))
)
const unidadDestinoObj = computed(() =>
  unidades.value.find(u => u.idUnidadMedida === Number(formConversion.idUnidadDestino))
)
const factorReciprocoCalculado = computed(() => {
  const f = Number(formConversion.factorConversion)
  if (!f || f <= 0) return 0
  return Number((1 / f).toFixed(6))
})

// ---------- Carga de Datos ----------
const cargarDatos = async () => {
  if (!can('UNIDADES', 'consultar')) {
    isLoading.value = false
    return
  }

  isLoading.value = true
  try {
    const [resU, resC] = await Promise.all([
      unidadService.obtenerUnidades(),
      unidadService.obtenerConversiones()
    ])
    unidades.value = Array.isArray(resU) ? resU : (resU?.data || [])
    conversiones.value = Array.isArray(resC) ? resC : (resC?.data || [])
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudieron sincronizar las unidades de medida.')
  } finally {
    isLoading.value = false
  }
}

// ---------- CRUD Unidades ----------
const limpiarErroresUnidad = () => {
  Object.keys(erroresUnidad).forEach(k => delete erroresUnidad[k])
}

const abrirModalCrearUnidad = () => {
  if (!can('UNIDADES', 'insertar')) return
  isEditingUnidad.value = false
  currentUnidadId.value = null
  unidadOriginal.value = null
  limpiarErroresUnidad()

  formUnidad.codigoMedida = ''
  formUnidad.nombreMedida = ''
  formUnidad.tipoMedida = 'Longitud'

  showModalUnidad.value = true
}

const abrirModalEditarUnidad = (u) => {
  if (!can('UNIDADES', 'modificar')) return
  isEditingUnidad.value = true
  currentUnidadId.value = u.idUnidadMedida
  limpiarErroresUnidad()

  unidadOriginal.value = {
    codigoMedida: u.codigoMedida,
    nombreMedida: u.nombreMedida,
    tipoMedida: u.tipoMedida,
    totalArticulosAsociados: u.totalArticulosAsociados || 0
  }

  formUnidad.codigoMedida = u.codigoMedida
  formUnidad.nombreMedida = u.nombreMedida
  formUnidad.tipoMedida = u.tipoMedida

  showModalUnidad.value = true
}

const validarFormularioUnidad = () => {
  limpiarErroresUnidad()
  const cod = formUnidad.codigoMedida?.trim().toUpperCase() || ''
  const nom = formUnidad.nombreMedida?.trim() || ''

  if (!cod) {
    erroresUnidad.codigoMedida = 'El código o símbolo es obligatorio.'
  } else if (!/^[A-Z0-9_-]{1,10}$/.test(cod)) {
    erroresUnidad.codigoMedida = 'Debe tener entre 1 y 10 caracteres alfanuméricos sin espacios.'
  }

  if (!nom) {
    erroresUnidad.nombreMedida = 'El nombre de la unidad es obligatorio.'
  } else if (!TextRules.esNombreValido(nom, 2, 50)) {
    erroresUnidad.nombreMedida = 'Nombre inválido (entre 2 y 50 caracteres alfanuméricos).'
  }

  return Object.keys(erroresUnidad).length === 0
}

const solicitarGuardarUnidad = () => {
  if (!validarFormularioUnidad()) return

  const cod = formUnidad.codigoMedida.trim().toUpperCase()
  const nom = formUnidad.nombreMedida.trim()
  const tipo = formUnidad.tipoMedida

  if (isEditingUnidad.value) {
    const codAnt = unidadOriginal.value.codigoMedida
    const nomAnt = unidadOriginal.value.nombreMedida
    const tipoAnt = unidadOriginal.value.tipoMedida

    if (cod === codAnt && nom === nomAnt && tipo === tipoAnt) {
      mostrarToast('warning', 'No se detectaron modificaciones en la unidad.')
      return
    }

    confirmModalConfig.title = '¿Guardar cambios en la unidad?'
    confirmModalConfig.subtitle = 'Revisa las especificaciones modificadas antes de actualizar en la base de datos.'
    confirmModalConfig.type = 'unidad_diff'
    confirmModalConfig.detalles = [
      { etiqueta: 'Símbolo / Código', anterior: codAnt, nuevo: cod, haCambiado: cod !== codAnt },
      { etiqueta: 'Nombre de Unidad', anterior: nomAnt, nuevo: nom, haCambiado: nom !== nomAnt },
      { etiqueta: 'Dimensión Física', anterior: tipoAnt, nuevo: tipo, haCambiado: tipo !== tipoAnt }
    ]
  } else {
    confirmModalConfig.title = '¿Registrar nueva unidad de medida?'
    confirmModalConfig.subtitle = 'Esta unidad estará disponible para asignación en el catálogo de artículos y recetas.'
    confirmModalConfig.type = 'unidad_insert'
    confirmModalConfig.detalles = [
      { etiqueta: 'Código / Símbolo', nuevo: cod },
      { etiqueta: 'Nombre Completo', nuevo: nom },
      { etiqueta: 'Dimensión Física', nuevo: tipo }
    ]
  }

  confirmModalConfig.onConfirm = ejecutarGuardadoUnidad
  showConfirmModal.value = true
}

const ejecutarGuardadoUnidad = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const payload = {
    codigoMedida: formUnidad.codigoMedida.trim().toUpperCase(),
    nombreMedida: formUnidad.nombreMedida.trim(),
    tipoMedida: formUnidad.tipoMedida
  }

  try {
    if (isEditingUnidad.value) {
      await unidadService.actualizarUnidad(currentUnidadId.value, payload)
      mostrarToast('success', `Unidad "${payload.nombreMedida}" actualizada exitosamente.`)
    } else {
      await unidadService.crearUnidad(payload)
      mostrarToast('success', `Unidad "${payload.nombreMedida}" registrada en el catálogo.`)
    }
    showConfirmModal.value = false
    showModalUnidad.value = false
    await cargarDatos()
  } catch (err) {
    showConfirmModal.value = false
    const msg = err?.response?.data?.message || 'Error al procesar la unidad de medida.'
    erroresUnidad.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

// ---------- CRUD Conversiones ----------
const limpiarErroresConversion = () => {
  Object.keys(erroresConversion).forEach(k => delete erroresConversion[k])
}

const abrirModalCrearConversion = () => {
  if (!can('UNIDADES', 'insertar')) return
  isEditingConversion.value = false
  conversionOriginal.value = null
  limpiarErroresConversion()

  const primeraUnidad = unidades.value[0]?.idUnidadMedida || ''
  formConversion.idUnidadOrigen = primeraUnidad
  formConversion.factorConversion = 1

  const compatibles = unidades.value.filter(u => u.tipoMedida === unidades.value[0]?.tipoMedida && u.idUnidadMedida !== primeraUnidad)
  formConversion.idUnidadDestino = compatibles[0]?.idUnidadMedida || ''

  showModalConversion.value = true
}

const abrirModalEditarConversion = (c) => {
  if (!can('UNIDADES', 'modificar')) return
  isEditingConversion.value = true
  limpiarErroresConversion()

  conversionOriginal.value = {
    idUnidadOrigen: c.idUnidadOrigen,
    idUnidadDestino: c.idUnidadDestino,
    nombreOrigen: c.nombreOrigen,
    codigoOrigen: c.codigoOrigen,
    nombreDestino: c.nombreDestino,
    codigoDestino: c.codigoDestino,
    factorConversion: c.factorConversion
  }

  formConversion.idUnidadOrigen = c.idUnidadOrigen
  formConversion.idUnidadDestino = c.idUnidadDestino
  formConversion.factorConversion = c.factorConversion

  showModalConversion.value = true
}

const onUnidadOrigenChange = () => {
  const compatibles = unidadesDestinoCompatibles.value
  if (!compatibles.some(u => u.idUnidadMedida === Number(formConversion.idUnidadDestino))) {
    formConversion.idUnidadDestino = compatibles[0]?.idUnidadMedida || ''
  }
}

const validarFormularioConversion = () => {
  limpiarErroresConversion()

  if (!formConversion.idUnidadOrigen) {
    erroresConversion.idUnidadOrigen = 'Selecciona la unidad origen.'
  }
  if (!formConversion.idUnidadDestino) {
    erroresConversion.idUnidadDestino = 'Selecciona la unidad destino compatible.'
  }
  if (Number(formConversion.idUnidadOrigen) === Number(formConversion.idUnidadDestino)) {
    erroresConversion.idUnidadDestino = 'La unidad origen y destino no pueden ser la misma.'
  }
  if (Number(formConversion.factorConversion) <= 0) {
    erroresConversion.factorConversion = 'El factor multiplicador debe ser estrictamente mayor a 0.'
  }

  return Object.keys(erroresConversion).length === 0
}

const solicitarGuardarConversion = () => {
  if (!validarFormularioConversion()) return

  const origen = unidadOrigenObj.value
  const destino = unidadDestinoObj.value
  const factor = Number(formConversion.factorConversion)
  const reciproco = factorReciprocoCalculado.value

  confirmModalConfig.title = isEditingConversion.value
    ? '¿Actualizar factor de conversión?'
    : '¿Confirmar nueva regla de conversión?'
  confirmModalConfig.subtitle = 'Se registrará la relación directa y su equivalencia inversa calculada automáticamente.'
  confirmModalConfig.type = 'conversion_resumen'
  confirmModalConfig.detalles = [
    {
      etiqueta: 'Relación Directa',
      valor: `1.00 ${origen?.codigoMedida} (${origen?.nombreMedida}) = ${factor.toFixed(6)} ${destino?.codigoMedida} (${destino?.nombreMedida})`
    },
    {
      etiqueta: 'Relación Inversa (Recíproco)',
      valor: `1.00 ${destino?.codigoMedida} (${destino?.nombreMedida}) = ${reciproco.toFixed(6)} ${origen?.codigoMedida} (${origen?.nombreMedida})`
    },
    {
      etiqueta: 'Dimensión Validada',
      valor: `${origen?.tipoMedida} (Compatible)`
    }
  ]

  confirmModalConfig.onConfirm = ejecutarGuardadoConversion
  showConfirmModal.value = true
}

const ejecutarGuardadoConversion = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const payload = {
    idUnidadOrigen: Number(formConversion.idUnidadOrigen),
    idUnidadDestino: Number(formConversion.idUnidadDestino),
    factorConversion: Number(formConversion.factorConversion)
  }

  try {
    await unidadService.guardarConversion(payload)
    mostrarToast('success', 'Factor de conversión y equivalencia recíproca guardados exitosamente.')
    showConfirmModal.value = false
    showModalConversion.value = false
    await cargarDatos()
  } catch (err) {
    showConfirmModal.value = false
    const msg = err?.response?.data?.message || 'Error al registrar la conversión.'
    erroresConversion.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

const solicitarEliminarConversion = async (c) => {
  if (!can('UNIDADES', 'eliminar')) return

  const confirmado = await pedirConfirmacion({
    variant: 'danger',
    title: '¿Eliminar factor de conversión?',
    message: `¿Estás seguro de remover la equivalencia de ${c.nombreOrigen} (${c.codigoOrigen}) hacia ${c.nombreDestino} (${c.codigoDestino})?`,
    detalles: [
      `Relación directa: 1 ${c.codigoOrigen} = ${c.factorConversion.toFixed(6)} ${c.codigoDestino}`,
      'Aviso: La regla inversa complementaria también será eliminada del sistema.'
    ],
    confirmText: 'Sí, eliminar conversión',
    cancelText: 'Cancelar'
  })

  if (!confirmado) return

  try {
    const res = await unidadService.eliminarConversion(c.idUnidadOrigen, c.idUnidadDestino)
    mostrarToast('success', res?.message || 'Regla de conversión eliminada correctamente.')
    await cargarDatos()
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo eliminar la conversión.')
  }
}

// Control Teclado (Escape)
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) resolverConfirmacion(false)
  else if (showConfirmModal.value) showConfirmModal.value = false
  else if (showModalConversion.value) showModalConversion.value = false
  else if (showModalUnidad.value) showModalUnidad.value = false
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
    <!-- Modal Central de Confirmación (Diff y Resumen Estructurado) -->
    <Transition name="fade">
      <div v-if="showConfirmModal" class="fixed inset-0 z-[65] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4" role="dialog" aria-modal="true">
        <div class="bg-white rounded-3xl p-6 sm:p-7 shadow-2xl w-full max-w-md border border-slate-100 text-center animate-scale-in max-h-[90vh] flex flex-col">
          <div class="w-16 h-16 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center mx-auto mb-4 border border-purple-100 shadow-sm shrink-0">
            <svg class="w-8 h-8 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
            </svg>
          </div>

          <h3 class="text-lg font-black text-slate-800 tracking-tight shrink-0">{{ confirmModalConfig.title }}</h3>
          <p class="text-xs text-slate-500 font-medium mt-1 mb-4 shrink-0">{{ confirmModalConfig.subtitle }}</p>

          <!-- Vista 1: Diff de Unidad -->
          <div v-if="confirmModalConfig.type === 'unidad_diff'" class="flex-1 overflow-y-auto text-left space-y-3 mb-5 bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
            <div v-for="d in confirmModalConfig.detalles" :key="d.etiqueta" class="text-xs pb-2.5 last:pb-0 border-b border-slate-200/70 last:border-0">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-slate-500 uppercase text-[10px] tracking-wider">{{ d.etiqueta }}</span>
                <span :class="['text-[10px] font-bold px-2 py-0.5 rounded-full', d.haCambiado ? 'bg-purple-100 text-purple-700 border border-purple-200' : 'text-slate-400']">
                  {{ d.haCambiado ? 'Modificado' : 'Sin cambios' }}
                </span>
              </div>
              <div v-if="d.haCambiado">
                <p class="text-[11px] text-slate-400 line-through"><span class="font-semibold">Antes:</span> {{ d.anterior }}</p>
                <p class="text-xs font-bold text-slate-800"><span class="text-purple-600 font-bold">Ahora:</span> {{ d.nuevo }}</p>
              </div>
              <p v-else class="text-xs font-semibold text-slate-700">{{ d.nuevo }}</p>
            </div>
          </div>

          <!-- Vista 2: Resumen Inserción Unidad -->
          <div v-else-if="confirmModalConfig.type === 'unidad_insert'" class="flex-1 overflow-y-auto text-left space-y-2.5 mb-5 bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
            <div v-for="d in confirmModalConfig.detalles" :key="d.etiqueta" class="text-xs flex justify-between items-center pb-2 border-b border-slate-200/70 last:border-0">
              <span class="font-bold text-slate-500 uppercase text-[10px]">{{ d.etiqueta }}:</span>
              <span class="font-bold text-slate-800">{{ d.nuevo }}</span>
            </div>
          </div>

          <!-- Vista 3: Resumen Conversión & Recíproco -->
          <div v-else class="flex-1 overflow-y-auto text-left space-y-3 mb-5 bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
            <div v-for="d in confirmModalConfig.detalles" :key="d.etiqueta" class="text-xs pb-2.5 last:pb-0 border-b border-slate-200/70 last:border-0">
              <span class="font-bold text-slate-500 uppercase text-[10px] block mb-0.5">{{ d.etiqueta }}:</span>
              <span class="font-bold font-mono text-purple-900 text-xs break-words">{{ d.valor }}</span>
            </div>
          </div>

          <div class="flex items-center gap-3 shrink-0 pt-2 border-t border-slate-100">
            <button
              type="button"
              @click="showConfirmModal = false"
              :disabled="isSaving"
              class="flex-1 py-2.5 px-4 text-xs font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors disabled:opacity-50"
            >
              Revisar de nuevo
            </button>
            <button
              type="button"
              @click="confirmModalConfig.onConfirm"
              :disabled="isSaving"
              class="flex-1 py-2.5 px-4 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg transition-all disabled:opacity-60"
            >
              {{ isSaving ? 'Guardando...' : 'Sí, confirmar' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Diálogo Pop-in Reutilizable -->
    <Transition name="pop">
      <div
        v-if="confirmDialog.visible"
        class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm"
        role="alertdialog"
        aria-modal="true"
        @click.self="resolverConfirmacion(false)"
      >
        <div class="pop-card bg-white rounded-3xl shadow-2xl w-full max-w-sm overflow-hidden animate-scale-in">
          <div :class="['h-1.5 bg-gradient-to-r', CONFIRM_VARIANTS[confirmDialog.variant]?.bar || 'from-purple-600 to-pink-500']"></div>
          <div class="px-6 pt-6 pb-4 text-center">
            <div :class="['w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4', CONFIRM_VARIANTS[confirmDialog.variant]?.iconWrap]">
              <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="CONFIRM_VARIANTS[confirmDialog.variant]?.icon" />
              </svg>
            </div>
            <h3 class="font-black text-slate-800 text-base">{{ confirmDialog.title }}</h3>
            <p class="text-xs text-slate-500 font-medium mt-1.5 leading-relaxed whitespace-pre-line">{{ confirmDialog.message }}</p>

            <div
              v-if="confirmDialog.detalles.length"
              :class="['mt-4 border rounded-xl px-3 py-2.5 text-left space-y-1 max-h-40 overflow-y-auto', CONFIRM_VARIANTS[confirmDialog.variant]?.panel]"
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
              :class="['flex-1 px-4 py-2.5 text-xs font-bold text-white rounded-xl shadow-md transition-all', CONFIRM_VARIANTS[confirmDialog.variant]?.button]"
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
    <div v-if="!can('UNIDADES', 'consultar')" class="bg-white rounded-3xl p-14 text-center shadow-md border border-slate-100">
      <div class="w-16 h-16 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4 ring-8 ring-rose-50/60">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>
      </div>
      <h3 class="text-base font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-sm text-slate-500 mt-1.5 max-w-sm mx-auto">No cuentas con autorización para consultar unidades de medida y factores de conversión.</p>
    </div>

    <!-- Contenedor Principal -->
    <div v-else class="space-y-6">
      <!-- Encabezado con Botón Principal -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Unidades de Medida y Conversiones</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de dimensiones físicas base y factores de transformación recíprocos para confección</p>
        </div>
        
        <div>
          <button 
            v-if="activeTab === 'unidades' && can('UNIDADES', 'insertar')"
            @click="abrirModalCrearUnidad"
            class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:scale-[1.01] active:scale-[0.98] transition-all whitespace-nowrap"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" /></svg>
            Nueva Unidad
          </button>
          <button 
            v-if="activeTab === 'conversiones' && can('UNIDADES', 'insertar')"
            @click="abrirModalCrearConversion"
            class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:scale-[1.01] active:scale-[0.98] transition-all whitespace-nowrap"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" /></svg>
            Nueva Conversión
          </button>
        </div>
      </div>

      <!-- Tarjetas KPIs Informativas -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Unidades Base</p>
            <p class="text-2xl font-black text-slate-900 mt-1 font-mono">{{ kpis.totalUnidades }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center border border-purple-100 shadow-sm font-bold">
            📐
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Reglas Conversión</p>
            <p class="text-2xl font-black text-purple-700 mt-1 font-mono">{{ kpis.totalConversiones }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center border border-purple-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Dimensiones Físicas</p>
            <p class="text-2xl font-black text-sky-600 mt-1 font-mono">{{ kpis.dimensiones }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-sky-50 text-sky-600 flex items-center justify-center border border-sky-100 shadow-sm">
            <span class="text-xs font-mono font-black">4D</span>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Artículos con Medida</p>
            <p class="text-2xl font-black text-emerald-600 mt-1 font-mono">{{ kpis.articulosAsociados }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center border border-emerald-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" /></svg>
          </div>
        </div>
      </div>

      <!-- Barra de Pestañas Estilizada -->
      <div class="flex items-center gap-2 p-1.5 bg-slate-100/90 rounded-2xl border border-slate-200/80 w-fit">
        <button 
          @click="activeTab = 'unidades'"
          :class="[
            'px-5 py-2.5 rounded-xl text-xs font-black transition-all flex items-center gap-2',
            activeTab === 'unidades'
              ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-md shadow-purple-500/25'
              : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/60'
          ]"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" /></svg>
          <span>Catálogo de Unidades ({{ unidades.length }})</span>
        </button>
        <button 
          @click="activeTab = 'conversiones'"
          :class="[
            'px-5 py-2.5 rounded-xl text-xs font-black transition-all flex items-center gap-2',
            activeTab === 'conversiones'
              ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-md shadow-purple-500/25'
              : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/60'
          ]"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" /></svg>
          <span>Factores de Conversión ({{ conversiones.length }})</span>
        </button>
      </div>

      <!-- ========================================== -->
      <!-- PESTAÑA 1: CATÁLOGO DE UNIDADES            -->
      <!-- ========================================== -->
      <div v-if="activeTab === 'unidades'" class="space-y-4">
        <!-- Filtros Unidades -->
        <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 flex flex-col sm:flex-row items-center gap-3">
          <div class="relative flex-1 w-full">
            <input 
              v-model="filtroUnidad" 
              type="text" 
              placeholder="Buscar unidad por nombre o código (ej. YD, Metro, Libra)..." 
              class="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all" 
            />
            <svg class="w-4 h-4 text-slate-400 absolute left-3.5 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" /></svg>
          </div>

          <div class="w-full sm:w-64">
            <select 
              v-model="filtroTipo" 
              class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-700 outline-none focus:bg-white focus:border-purple-500"
            >
              <option value="">Todas las dimensiones</option>
              <option value="Longitud">Longitud (Telas, Cintas)</option>
              <option value="Masa">Masa (Hilos, Lana)</option>
              <option value="Unidad">Unidad (Botones, Prendas)</option>
              <option value="Volumen">Volumen (Tintes, Químicos)</option>
            </select>
          </div>
        </div>

        <!-- Tabla Unidades -->
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-100">
            <h3 class="text-sm font-black text-slate-800">Catálogo Maestro de Unidades</h3>
            <span v-if="!isLoading" class="text-xs font-semibold text-slate-500">
              {{ unidadesFiltradas.length }} {{ unidadesFiltradas.length === 1 ? 'unidad' : 'unidades' }}
            </span>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full min-w-[780px] text-left border-collapse">
              <thead>
                <tr class="bg-slate-50/70 border-b border-slate-100 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                  <th class="py-3.5 px-6 whitespace-nowrap min-w-[130px]">Código / Símbolo</th>
                  <th class="py-3.5 px-4 min-w-[200px]">Nombre de la Unidad</th>
                  <th class="py-3.5 px-4 whitespace-nowrap min-w-[160px]">Dimensión Física</th>
                  <th class="py-3.5 px-4 text-center whitespace-nowrap min-w-[160px]">Artículos Vinculados</th>
                  <th class="py-3.5 px-6 text-right whitespace-nowrap min-w-[120px]">Acciones</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-if="isLoading">
                  <td colspan="5" class="py-14 text-center text-slate-400 font-semibold">
                    Cargando catálogo de unidades...
                  </td>
                </tr>

                <tr v-for="u in unidadesFiltradas" :key="u.idUnidadMedida" class="hover:bg-purple-50/20 transition-colors">
                  <!-- Código -->
                  <td class="py-3.5 px-6 whitespace-nowrap">
                    <span class="inline-block px-2 py-1 rounded-md bg-purple-50 text-purple-700 font-mono font-black text-xs border border-purple-200">
                      {{ u.codigoMedida }}
                    </span>
                  </td>

                  <!-- Nombre -->
                  <td class="py-3.5 px-4 font-bold text-slate-900 text-[13px]">
                    {{ u.nombreMedida }}
                  </td>

                  <!-- Dimensión -->
                  <td class="py-3.5 px-4 whitespace-nowrap">
                    <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[10px] font-bold border shadow-2xs', getTipoBadgeClass(u.tipoMedida)]">
                      <span :class="['w-1.5 h-1.5 rounded-full', getTipoDotClass(u.tipoMedida)]"></span>
                      {{ u.tipoMedida }}
                    </span>
                  </td>

                  <!-- Artículos Vinculados -->
                  <td class="py-3.5 px-4 text-center whitespace-nowrap">
                    <span :class="[
                      'px-2.5 py-1 rounded-full text-[11px] font-bold font-mono border shadow-2xs',
                      u.totalArticulosAsociados > 0 
                        ? 'bg-purple-50 text-purple-700 border-purple-200' 
                        : 'bg-slate-50 text-slate-400 border-slate-200'
                    ]">
                      {{ u.totalArticulosAsociados }} artículo(s)
                    </span>
                  </td>

                  <!-- Acciones -->
                  <td class="py-3.5 px-6 text-right whitespace-nowrap">
                    <button 
                      v-if="can('UNIDADES', 'modificar')"
                      @click="abrirModalEditarUnidad(u)"
                      class="px-3 py-1.5 bg-slate-100 hover:bg-purple-600 hover:text-white text-slate-700 rounded-lg text-xs font-bold transition-all shadow-xs border border-slate-200"
                    >
                      Editar
                    </button>
                  </td>
                </tr>

                <tr v-if="unidadesFiltradas.length === 0 && !isLoading">
                  <td colspan="5" class="py-14 text-center">
                    <div class="w-14 h-14 bg-purple-50 text-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-3 border border-purple-100">
                      <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" /></svg>
                    </div>
                    <p class="text-sm font-black text-slate-800">No se encontraron unidades registradas</p>
                    <p class="text-xs text-slate-500 font-medium mt-1">Ajusta los filtros o registra una nueva unidad de medida.</p>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- ========================================== -->
      <!-- PESTAÑA 2: MATRIZ DE CONVERSIONES          -->
      <!-- ========================================== -->
      <div v-if="activeTab === 'conversiones'" class="space-y-4">
        <!-- Buscador Conversiones -->
        <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100">
          <div class="relative w-full">
            <input 
              v-model="filtroConversion" 
              type="text" 
              placeholder="Buscar por unidad, código o dimensión (ej: YD, Metro, Yarda, Kilogramo, Longitud)..." 
              class="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all" 
            />
            <svg class="w-4 h-4 text-slate-400 absolute left-3.5 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" /></svg>
          </div>
        </div>

        <!-- Tabla Conversiones -->
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-100">
            <h3 class="text-sm font-black text-slate-800">Matriz de Factores y Equivalencias</h3>
            <span v-if="!isLoading" class="text-xs font-semibold text-slate-500">
              {{ conversionesFiltradas.length }} {{ conversionesFiltradas.length === 1 ? 'regla' : 'reglas' }}
            </span>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full min-w-[880px] text-left border-collapse">
              <thead>
                <tr class="bg-slate-50/70 border-b border-slate-100 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                  <th class="py-3.5 px-6 whitespace-nowrap min-w-[200px]">Unidad Base (1.00)</th>
                  <th class="py-3.5 px-4 text-center whitespace-nowrap min-w-[80px]">Operación</th>
                  <th class="py-3.5 px-4 min-w-[200px]">Unidad Destino</th>
                  <th class="py-3.5 px-4 whitespace-nowrap min-w-[140px]">Dimensión</th>
                  <th class="py-3.5 px-4 text-right whitespace-nowrap min-w-[160px]">Factor Multiplicador</th>
                  <th class="py-3.5 px-6 text-right whitespace-nowrap min-w-[150px]">Acciones</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-if="isLoading">
                  <td colspan="6" class="py-14 text-center text-slate-400 font-semibold">
                    Cargando factores de conversión...
                  </td>
                </tr>

                <tr v-for="c in conversionesFiltradas" :key="`${c.idUnidadOrigen}-${c.idUnidadDestino}`" class="hover:bg-purple-50/20 transition-colors">
                  <!-- Unidad Base -->
                  <td class="py-3.5 px-6 whitespace-nowrap font-bold text-slate-900">
                    1.00 <span class="font-mono text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-200">({{ c.codigoOrigen }})</span> {{ c.nombreOrigen }}
                  </td>

                  <!-- Operación -->
                  <td class="py-3.5 px-4 text-center text-slate-400 font-bold whitespace-nowrap">
                    <span class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-slate-100 text-slate-500 text-xs">
                      ➔
                    </span>
                  </td>

                  <!-- Unidad Destino -->
                  <td class="py-3.5 px-4 font-bold text-slate-900 whitespace-nowrap">
                    <span class="font-mono text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-200">({{ c.codigoDestino }})</span> {{ c.nombreDestino }}
                  </td>

                  <!-- Dimensión -->
                  <td class="py-3.5 px-4 whitespace-nowrap">
                    <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[10px] font-bold border shadow-2xs', getTipoBadgeClass(c.tipoMedida)]">
                      <span :class="['w-1.5 h-1.5 rounded-full', getTipoDotClass(c.tipoMedida)]"></span>
                      {{ c.tipoMedida }}
                    </span>
                  </td>

                  <!-- Factor -->
                  <td class="py-3.5 px-4 text-right font-mono font-black text-slate-800 text-[13px] whitespace-nowrap">
                    {{ Number(c.factorConversion).toFixed(6) }}
                  </td>

                  <!-- Acciones -->
                  <td class="py-3.5 px-6 text-right whitespace-nowrap">
                    <div class="flex items-center justify-end gap-1.5">
                      <button 
                        v-if="can('UNIDADES', 'modificar')"
                        @click="abrirModalEditarConversion(c)"
                        class="px-2.5 py-1.5 bg-slate-100 hover:bg-purple-600 hover:text-white text-slate-700 rounded-lg text-xs font-bold transition-all shadow-xs border border-slate-200"
                        title="Modificar factor de conversión"
                      >
                        Editar
                      </button>
                      <button 
                        v-if="can('UNIDADES', 'eliminar')"
                        @click="solicitarEliminarConversion(c)"
                        class="px-2.5 py-1.5 bg-rose-50 hover:bg-rose-600 hover:text-white text-rose-700 rounded-lg text-xs font-bold transition-all border border-rose-200 shadow-xs"
                        title="Eliminar factor y su recíproco"
                      >
                        Eliminar
                      </button>
                    </div>
                  </td>
                </tr>

                <tr v-if="conversionesFiltradas.length === 0 && !isLoading">
                  <td colspan="6" class="py-14 text-center">
                    <div class="w-14 h-14 bg-purple-50 text-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-3 border border-purple-100">
                      <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" /></svg>
                    </div>
                    <p class="text-sm font-black text-slate-800">No se encontraron factores de conversión</p>
                    <p class="text-xs text-slate-500 font-medium mt-1">Registra una nueva regla multiplicadora entre unidades.</p>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Formulario de Unidad (Crear / Editar) -->
    <Transition name="fade">
      <div v-if="showModalUnidad" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-3xl shadow-2xl w-full max-w-sm border border-slate-100 overflow-hidden animate-scale-in">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <div class="px-6 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">
                {{ isEditingUnidad ? 'Editar Unidad de Medida' : 'Nueva Unidad de Medida' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Símbolo y clasificación dimensional estándar</p>
            </div>
            <button @click="showModalUnidad = false" :disabled="isSaving" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <form @submit.prevent="solicitarGuardarUnidad" class="p-6 space-y-4">
            <div v-if="erroresUnidad.general" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-bold rounded-xl px-4 py-3">
              {{ erroresUnidad.general }}
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Código (Símbolo) <span class="text-rose-500">*</span>
                </label>
                <input 
                  v-model="formUnidad.codigoMedida" 
                  @keypress="allowOnly.codigoInput($event)"
                  required 
                  maxlength="10"
                  type="text" 
                  placeholder="YD" 
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-800 uppercase focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition" 
                />
                <p v-if="erroresUnidad.codigoMedida" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresUnidad.codigoMedida }}
                </p>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Dimensión <span class="text-rose-500">*</span>
                </label>
                <select 
                  v-model="formUnidad.tipoMedida" 
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition"
                >
                  <option value="Longitud">Longitud</option>
                  <option value="Masa">Masa</option>
                  <option value="Unidad">Unidad</option>
                  <option value="Volumen">Volumen</option>
                </select>
              </div>
            </div>

            <div>
              <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                Nombre Completo <span class="text-rose-500">*</span>
              </label>
              <input 
                v-model="formUnidad.nombreMedida" 
                @keypress="allowOnly.nombreInput($event)"
                required 
                maxlength="50"
                type="text" 
                placeholder="Ej: Yarda o Kilogramo" 
                class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition" 
              />
              <p v-if="erroresUnidad.nombreMedida" class="text-rose-600 text-[11px] font-bold mt-1">
                {{ erroresUnidad.nombreMedida }}
              </p>
            </div>

            <div v-if="isEditingUnidad && unidadOriginal?.totalArticulosAsociados > 0" class="p-3 bg-amber-50 rounded-xl border border-amber-200 text-xs text-amber-800 font-medium">
              ⚠️ Esta unidad tiene <b class="font-bold">{{ unidadOriginal.totalArticulosAsociados }}</b> artículo(s) asignado(s). Cambiar la dimensión puede alterar conversiones de inventario.
            </div>

            <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
              <button 
                type="button" 
                @click="showModalUnidad = false" 
                :disabled="isSaving"
                class="px-4 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100 rounded-xl transition"
              >
                Cancelar
              </button>
              <button 
                type="submit" 
                :disabled="isSaving"
                class="px-4 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition disabled:opacity-50"
              >
                {{ isSaving ? 'Guardando...' : (isEditingUnidad ? 'Revisar y Guardar' : 'Revisar y Registrar') }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Transition>

    <!-- Modal Formulario Factor Conversión (Creación / Edición) -->
    <Transition name="fade">
      <div v-if="showModalConversion" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-3xl shadow-2xl w-full max-w-lg border border-slate-100 overflow-hidden animate-scale-in">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <div class="px-6 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">
                {{ isEditingConversion ? 'Modificar Factor de Conversión' : 'Nueva Regla de Conversión' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">El sistema calcula y actualiza la relación inversa recíproca automáticamente</p>
            </div>
            <button @click="showModalConversion = false" :disabled="isSaving" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <form @submit.prevent="solicitarGuardarConversion" class="p-6 space-y-4">
            <div v-if="erroresConversion.general" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-bold rounded-xl px-4 py-3">
              {{ erroresConversion.general }}
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Unidad Origen (1.00) <span class="text-rose-500">*</span>
                </label>
                <select 
                  v-model="formConversion.idUnidadOrigen" 
                  @change="onUnidadOrigenChange"
                  :disabled="isEditingConversion"
                  required 
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition disabled:opacity-60 disabled:cursor-not-allowed"
                >
                  <option v-for="u in unidades" :key="u.idUnidadMedida" :value="u.idUnidadMedida">
                    {{ u.nombreMedida }} ({{ u.codigoMedida }}) · {{ u.tipoMedida }}
                  </option>
                </select>
                <p v-if="erroresConversion.idUnidadOrigen" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresConversion.idUnidadOrigen }}
                </p>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Unidad Destino <span class="text-rose-500">*</span>
                </label>
                <select 
                  v-model="formConversion.idUnidadDestino" 
                  :disabled="isEditingConversion"
                  required 
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition disabled:opacity-60 disabled:cursor-not-allowed"
                >
                  <option v-for="u in unidadesDestinoCompatibles" :key="u.idUnidadMedida" :value="u.idUnidadMedida">
                    {{ u.nombreMedida }} ({{ u.codigoMedida }})
                  </option>
                </select>
                <p v-if="erroresConversion.idUnidadDestino" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresConversion.idUnidadDestino }}
                </p>
              </div>
            </div>

            <div>
              <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                Factor Multiplicador <span class="text-rose-500">*</span>
              </label>
              <input 
                v-model.number="formConversion.factorConversion" 
                required 
                type="number" 
                step="0.000001" 
                min="0.000001" 
                placeholder="0.914400"
                class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-black text-slate-900 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition" 
              />
              <p v-if="erroresConversion.factorConversion" class="text-rose-600 text-[11px] font-bold mt-1">
                {{ erroresConversion.factorConversion }}
              </p>
            </div>

            <!-- Panel de Previsualización Matemática de Equivalencia en Vivo -->
            <div v-if="unidadOrigenObj && unidadDestinoObj && Number(formConversion.factorConversion) > 0" class="p-3.5 bg-purple-50/70 rounded-2xl border border-purple-200 space-y-2 text-xs">
              <span class="text-[10px] font-black uppercase tracking-wider text-purple-800 block">
                ⚡ Previsualización de Equivalencias Recíprocas:
              </span>
              <div class="flex items-center justify-between font-mono bg-white p-2 rounded-xl border border-purple-100 shadow-2xs">
                <span class="text-slate-600 font-semibold">1.00 {{ unidadOrigenObj.codigoMedida }} =</span>
                <span class="font-bold text-purple-700">{{ Number(formConversion.factorConversion).toFixed(6) }} {{ unidadDestinoObj.codigoMedida }}</span>
              </div>
              <div class="flex items-center justify-between font-mono bg-white p-2 rounded-xl border border-purple-100 shadow-2xs">
                <span class="text-slate-600 font-semibold">1.00 {{ unidadDestinoObj.codigoMedida }} =</span>
                <span class="font-bold text-emerald-700">{{ factorReciprocoCalculado.toFixed(6) }} {{ unidadOrigenObj.codigoMedida }} (Recíproco automático)</span>
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
              <button 
                type="button" 
                @click="showModalConversion = false" 
                :disabled="isSaving"
                class="px-4 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100 rounded-xl transition"
              >
                Cancelar
              </button>
              <button 
                type="submit" 
                :disabled="isSaving"
                class="px-4 py-2 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md transition disabled:opacity-50"
              >
                {{ isSaving ? 'Guardando...' : (isEditingConversion ? 'Revisar y Guardar' : 'Revisar y Registrar') }}
              </button>
            </div>
          </form>
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