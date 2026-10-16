<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import articuloService from '../services/articuloService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

// ---------- Prefijos de Códigos Automáticos ----------
const PREFIJOS_TIPO = {
  MateriaPrima: 'TEL',
  Insumo: 'INS',
  PrendaTerminada: 'PRE',
  PiezaCorte: 'COR'
}

// ---------- Estado Base ----------
const articulos = ref([])
const unidadesMedida = ref([])
const isLoading = ref(true)
const isSaving = ref(false)

// Modales
const showModal = ref(false)
const showBodegasModal = ref(false)
const isEditing = ref(false)
const currentId = ref(null)
const articuloSeleccionadoBodegas = ref(null)
const articuloOriginal = ref(null)
const showConfirmGuardar = ref(false)

// Filtros
const filtroBusqueda = ref('')
const filtroTipo = ref('')
const mostrarInactivos = ref(false)

// Formulario reactivo
const formArticulo = reactive({
  codigoArticulo: '',
  nombreArticulo: '',
  tipoArticulo: 'MateriaPrima',
  idUnidadBaseMedida: '',
  stockMinimo: 0,
  costoPromedio: 0,
  talla: '',
  color: ''
})
const erroresForm = reactive({})

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

const getTipoLabel = (tipo) => {
  switch (tipo) {
    case 'MateriaPrima': return 'Materia Prima'
    case 'Insumo': return 'Insumo'
    case 'PrendaTerminada': return 'Prenda Terminada'
    case 'PiezaCorte': return 'Pieza de Corte'
    default: return tipo
  }
}

const getTipoBadgeClass = (tipo) => {
  switch (tipo) {
    case 'MateriaPrima': return 'bg-purple-50 text-purple-700 border-purple-200'
    case 'Insumo': return 'bg-sky-50 text-sky-700 border-sky-200'
    case 'PrendaTerminada': return 'bg-emerald-50 text-emerald-700 border-emerald-200'
    case 'PiezaCorte': return 'bg-amber-50 text-amber-700 border-amber-200'
    default: return 'bg-slate-50 text-slate-700 border-slate-200'
  }
}

const getTipoDotClass = (tipo) => {
  switch (tipo) {
    case 'MateriaPrima': return 'bg-purple-500'
    case 'Insumo': return 'bg-sky-500'
    case 'PrendaTerminada': return 'bg-emerald-500'
    case 'PiezaCorte': return 'bg-amber-500'
    default: return 'bg-slate-400'
  }
}

// ---------- Generación Automática de Códigos Correlativos ----------
const generarSiguienteCodigo = (tipo) => {
  const prefijo = PREFIJOS_TIPO[tipo] || 'ART'
  const regex = new RegExp(`^${prefijo}-(\\d+)$`, 'i')
  let maxNumero = 0

  articulos.value.forEach(art => {
    const match = art.codigoArticulo?.match(regex)
    if (match) {
      const num = parseInt(match[1], 10)
      if (!isNaN(num) && num > maxNumero) {
        maxNumero = num
      }
    }
  })

  const siguiente = maxNumero + 1
  return `${prefijo}-${String(siguiente).padStart(3, '0')}`
}

const aplicarCodigoSugerido = () => {
  formArticulo.codigoArticulo = generarSiguienteCodigo(formArticulo.tipoArticulo)
}

const aplicarUnidadPorDefecto = (tipo) => {
  if (tipo === 'PrendaTerminada') {
    const unidadPieza = unidadesMedida.value.find(u =>
      u.codigoMedida?.toUpperCase() === 'PZA' ||
      u.codigoMedida?.toUpperCase() === 'PZ' ||
      u.nombreMedida?.toLowerCase().includes('pieza') ||
      u.tipoMedida?.toLowerCase() === 'unidad'
    )
    if (unidadPieza) {
      formArticulo.idUnidadBaseMedida = unidadPieza.idUnidadMedida
    }
  }
}

const onTipoArticuloChange = () => {
  if (!isEditing.value) {
    aplicarCodigoSugerido()
  }
  aplicarUnidadPorDefecto(formArticulo.tipoArticulo)
}

// ---------- KPIs ----------
const kpis = computed(() => {
  const lista = articulos.value
  return {
    total: lista.length,
    materiasPrimas: lista.filter(a => a.tipoArticulo === 'MateriaPrima').length,
    prendas: lista.filter(a => a.tipoArticulo === 'PrendaTerminada').length,
    alertasStock: lista.filter(a => a.estadoArticulo && a.stockTotal <= a.stockMinimo).length
  }
})

// ---------- Filtrado ----------
const articulosFiltrados = computed(() => {
  return articulos.value.filter(a => {
    if (!mostrarInactivos.value && !a.estadoArticulo) return false
    if (filtroTipo.value && a.tipoArticulo !== filtroTipo.value) return false

    if (filtroBusqueda.value.trim()) {
      const term = filtroBusqueda.value.toLowerCase().trim()
      return (
        a.codigoArticulo.toLowerCase().includes(term) ||
        a.nombreArticulo.toLowerCase().includes(term) ||
        (a.talla && a.talla.toLowerCase().includes(term)) ||
        (a.color && a.color.toLowerCase().includes(term))
      )
    }
    return true
  })
})

// ---------- Comparación de Cambios (Diff) ----------
const resumenCambios = computed(() => {
  if (!isEditing.value || !articuloOriginal.value) return []

  const uAnt = unidadesMedida.value.find(u => u.idUnidadMedida === articuloOriginal.value.idUnidadBaseMedida)
  const uNue = unidadesMedida.value.find(u => u.idUnidadMedida === formArticulo.idUnidadBaseMedida)

  const campos = [
    {
      campo: 'codigoArticulo',
      etiqueta: 'Código único',
      anterior: articuloOriginal.value.codigoArticulo,
      nuevo: formArticulo.codigoArticulo.trim().toUpperCase(),
      haCambiado: formArticulo.codigoArticulo.trim().toUpperCase() !== articuloOriginal.value.codigoArticulo
    },
    {
      campo: 'nombreArticulo',
      etiqueta: 'Nombre del artículo',
      anterior: articuloOriginal.value.nombreArticulo,
      nuevo: formArticulo.nombreArticulo.trim(),
      haCambiado: formArticulo.nombreArticulo.trim() !== articuloOriginal.value.nombreArticulo
    },
    {
      campo: 'tipoArticulo',
      etiqueta: 'Clasificación',
      anterior: getTipoLabel(articuloOriginal.value.tipoArticulo),
      nuevo: getTipoLabel(formArticulo.tipoArticulo),
      haCambiado: formArticulo.tipoArticulo !== articuloOriginal.value.tipoArticulo
    },
    {
      campo: 'idUnidadBaseMedida',
      etiqueta: 'Unidad de medida',
      anterior: uAnt ? `${uAnt.nombreMedida} (${uAnt.codigoMedida})` : '—',
      nuevo: uNue ? `${uNue.nombreMedida} (${uNue.codigoMedida})` : '—',
      haCambiado: formArticulo.idUnidadBaseMedida !== articuloOriginal.value.idUnidadBaseMedida
    },
    {
      campo: 'stockMinimo',
      etiqueta: 'Stock mínimo',
      anterior: Number(articuloOriginal.value.stockMinimo).toFixed(2),
      nuevo: Number(formArticulo.stockMinimo || 0).toFixed(2),
      haCambiado: Number(formArticulo.stockMinimo || 0) !== Number(articuloOriginal.value.stockMinimo)
    },
    {
      campo: 'costoPromedio',
      etiqueta: 'Costo referencial',
      anterior: formatMoneda(articuloOriginal.value.costoPromedio),
      nuevo: formatMoneda(formArticulo.costoPromedio),
      haCambiado: Number(formArticulo.costoPromedio || 0) !== Number(articuloOriginal.value.costoPromedio)
    }
  ]

  if (formArticulo.tipoArticulo === 'PrendaTerminada') {
    campos.push(
      {
        campo: 'talla',
        etiqueta: 'Talla',
        anterior: articuloOriginal.value.talla || 'Sin talla',
        nuevo: formArticulo.talla?.trim() || 'Sin talla',
        haCambiado: (formArticulo.talla?.trim() || '') !== (articuloOriginal.value.talla || '')
      },
      {
        campo: 'color',
        etiqueta: 'Color',
        anterior: articuloOriginal.value.color || 'Sin color',
        nuevo: formArticulo.color?.trim() || 'Sin color',
        haCambiado: (formArticulo.color?.trim() || '') !== (articuloOriginal.value.color || '')
      }
    )
  }

  return campos
})

const cambiosDetectados = computed(() => resumenCambios.value.filter(c => c.haCambiado))

// ---------- Carga de Datos ----------
const cargarDatos = async () => {
  if (!can('ARTICULOS', 'consultar')) {
    isLoading.value = false
    return
  }

  isLoading.value = true
  try {
    const [resArticulos, resUnidades] = await Promise.all([
      articuloService.obtenerTodos(),
      articuloService.obtenerUnidadesMedida()
    ])
    articulos.value = Array.isArray(resArticulos) ? resArticulos : (resArticulos?.data || [])
    unidadesMedida.value = Array.isArray(resUnidades) ? resUnidades : (resUnidades?.data || [])
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudieron sincronizar los artículos con el servidor.')
  } finally {
    isLoading.value = false
  }
}

// ---------- Modales Crear / Editar ----------
const limpiarErrores = () => {
  Object.keys(erroresForm).forEach(k => delete erroresForm[k])
}

const abrirModalCrear = () => {
  if (!can('ARTICULOS', 'insertar')) return
  isEditing.value = false
  currentId.value = null
  articuloOriginal.value = null
  limpiarErrores()

  formArticulo.tipoArticulo = 'MateriaPrima'
  formArticulo.codigoArticulo = generarSiguienteCodigo('MateriaPrima')
  formArticulo.nombreArticulo = ''
  formArticulo.idUnidadBaseMedida = unidadesMedida.value[0]?.idUnidadMedida || ''
  formArticulo.stockMinimo = 0
  formArticulo.costoPromedio = 0
  formArticulo.talla = ''
  formArticulo.color = ''

  showModal.value = true
}

const abrirModalEditar = (item) => {
  if (!can('ARTICULOS', 'modificar')) return
  isEditing.value = true
  currentId.value = item.idArticulo
  limpiarErrores()

  articuloOriginal.value = {
    codigoArticulo: item.codigoArticulo,
    nombreArticulo: item.nombreArticulo,
    tipoArticulo: item.tipoArticulo,
    idUnidadBaseMedida: item.idUnidadBaseMedida,
    stockMinimo: item.stockMinimo,
    costoPromedio: item.costoPromedio,
    talla: item.talla || '',
    color: item.color || ''
  }

  formArticulo.codigoArticulo = item.codigoArticulo
  formArticulo.nombreArticulo = item.nombreArticulo
  formArticulo.tipoArticulo = item.tipoArticulo
  formArticulo.idUnidadBaseMedida = item.idUnidadBaseMedida
  formArticulo.stockMinimo = item.stockMinimo
  formArticulo.costoPromedio = item.costoPromedio
  formArticulo.talla = item.talla || ''
  formArticulo.color = item.color || ''

  showModal.value = true
}

const verDesgloseBodegas = (item) => {
  articuloSeleccionadoBodegas.value = item
  showBodegasModal.value = true
}

// ---------- Validaciones y Envío ----------
const validarFormulario = () => {
  limpiarErrores()
  const codigo = formArticulo.codigoArticulo?.trim().toUpperCase() || ''
  const nombre = formArticulo.nombreArticulo?.trim() || ''

  if (!codigo) {
    erroresForm.codigoArticulo = 'El código único es obligatorio.'
  } else if (!TextRules.esCodigoValido(codigo)) {
    erroresForm.codigoArticulo = 'Código inválido (2-30 caracteres alfanuméricos y guiones, sin espacios).'
  }

  if (!nombre) {
    erroresForm.nombreArticulo = 'El nombre del artículo es obligatorio.'
  } else if (!TextRules.esNombreValido(nombre, 3, 150)) {
    erroresForm.nombreArticulo = 'Nombre inválido (entre 3 y 150 caracteres legibles).'
  }

  if (!formArticulo.idUnidadBaseMedida) {
    erroresForm.idUnidadBaseMedida = 'Selecciona la unidad de medida base.'
  }

  if (Number(formArticulo.stockMinimo) < 0) {
    erroresForm.stockMinimo = 'El stock mínimo no puede ser negativo.'
  }

  if (Number(formArticulo.costoPromedio) < 0) {
    erroresForm.costoPromedio = 'El costo referencial no puede ser negativo.'
  }

  if (formArticulo.tipoArticulo === 'PrendaTerminada') {
    if (formArticulo.talla?.trim() && formArticulo.talla.trim().length > 10) {
      erroresForm.talla = 'La talla no puede superar 10 caracteres.'
    }
    if (formArticulo.color?.trim() && formArticulo.color.trim().length > 30) {
      erroresForm.color = 'El color no puede superar 30 caracteres.'
    }
  }

  return Object.keys(erroresForm).length === 0
}

const solicitarGuardar = () => {
  if (!validarFormulario()) return

  if (isEditing.value) {
    if (cambiosDetectados.value.length === 0) {
      mostrarToast('error', 'No realizaste ninguna modificación en el artículo.')
      return
    }
    showConfirmGuardar.value = true
  } else {
    ejecutarGuardado()
  }
}

const ejecutarGuardado = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const codigo = formArticulo.codigoArticulo.trim().toUpperCase()
  const nombre = formArticulo.nombreArticulo.trim()

  const payload = {
    codigoArticulo: codigo,
    nombreArticulo: nombre,
    tipoArticulo: formArticulo.tipoArticulo,
    idUnidadBaseMedida: Number(formArticulo.idUnidadBaseMedida),
    stockMinimo: Math.max(0, Number(formArticulo.stockMinimo || 0)),
    costoPromedio: Math.max(0, Number(formArticulo.costoPromedio || 0)),
    talla: formArticulo.tipoArticulo === 'PrendaTerminada' ? (formArticulo.talla?.trim() || null) : null,
    color: formArticulo.tipoArticulo === 'PrendaTerminada' ? (formArticulo.color?.trim() || null) : null
  }

  try {
    if (isEditing.value) {
      await articuloService.actualizar(currentId.value, payload)
      mostrarToast('success', `Artículo "${nombre}" actualizado exitosamente.`)
    } else {
      await articuloService.crear(payload)
      mostrarToast('success', `Artículo "${nombre}" registrado con código ${codigo}.`)
    }
    showConfirmGuardar.value = false
    showModal.value = false
    await cargarDatos()
  } catch (err) {
    showConfirmGuardar.value = false
    const msg = err?.response?.data?.message || 'Error al procesar el artículo.'
    erroresForm.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

// ---------- Cambio de Estado ----------
const solicitarToggleEstado = async (item) => {
  if (!can('ARTICULOS', 'eliminar')) return
  const accion = item.estadoArticulo ? 'desactivar' : 'activar'

  const confirmado = await pedirConfirmacion({
    variant: item.estadoArticulo ? 'danger' : 'primary',
    title: `¿Confirmar cambio de estado?`,
    message: `¿Deseas ${accion} el artículo "${item.nombreArticulo}" (${item.codigoArticulo})?`,
    detalles: [
      `Código: ${item.codigoArticulo}`,
      `Clasificación: ${getTipoLabel(item.tipoArticulo)}`,
      `Stock actual en planta: ${item.stockTotal.toFixed(2)} ${item.codigoMedida}`
    ],
    confirmText: `Sí, ${accion}`,
    cancelText: 'Cancelar'
  })

  if (!confirmado) return

  try {
    const res = await articuloService.cambiarEstado(item.idArticulo)
    mostrarToast('success', res?.message || `Artículo ${accion}do correctamente.`)
    await cargarDatos()
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo cambiar el estado del artículo.')
  }
}

// Control Teclado (Escape)
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) resolverConfirmacion(false)
  else if (showConfirmGuardar.value) showConfirmGuardar.value = false
  else if (showBodegasModal.value) showBodegasModal.value = false
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
    <!-- Modal Confirmación de Modificaciones (Diff Visual) -->
    <Transition name="fade">
      <div v-if="showConfirmGuardar" class="fixed inset-0 z-[65] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4" role="dialog" aria-modal="true">
        <div class="bg-white rounded-3xl p-6 sm:p-7 shadow-2xl w-full max-w-md border border-slate-100 text-center animate-scale-in">
          <div class="w-16 h-16 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center mx-auto mb-4 border border-purple-100 shadow-sm">
            <svg class="w-8 h-8 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>

          <h3 class="text-lg font-black text-slate-800 tracking-tight">¿Guardar los cambios?</h3>
          <p class="text-xs text-slate-500 font-medium mt-1 mb-5">
            Compara los valores modificados antes de actualizar la ficha del artículo.
          </p>

          <div class="bg-slate-50/80 border border-slate-200/70 rounded-2xl p-4 text-left space-y-3 mb-6 max-h-60 overflow-y-auto">
            <div
              v-for="item in resumenCambios"
              :key="item.campo"
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
              {{ isSaving ? 'Guardando...' : 'Sí, confirmar cambios' }}
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
    <div v-if="!can('ARTICULOS', 'consultar')" class="bg-white rounded-3xl p-14 text-center shadow-md border border-slate-100">
      <div class="w-16 h-16 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4 ring-8 ring-rose-50/60">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>
      </div>
      <h3 class="text-base font-bold text-slate-800">Acceso restringido</h3>
      <p class="text-sm text-slate-500 mt-1.5 max-w-sm mx-auto">No cuentas con autorización para consultar el inventario de artículos.</p>
    </div>

    <!-- Contenedor Principal -->
    <div v-else class="space-y-6">
      <!-- Encabezado con Botón Principal -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Catálogo de Artículos y Telas</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de materias primas, telas en rollo, avíos y prendas terminadas</p>
        </div>
        <button
          v-if="can('ARTICULOS', 'insertar')"
          @click="abrirModalCrear"
          class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:scale-[1.01] active:scale-[0.98] transition-all whitespace-nowrap"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" /></svg>
          Nuevo Artículo
        </button>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Total Catálogo</p>
            <p class="text-2xl font-black text-slate-900 mt-1 font-mono">{{ kpis.total }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center border border-purple-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Materias Primas / Telas</p>
            <p class="text-2xl font-black text-purple-700 mt-1 font-mono">{{ kpis.materiasPrimas }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center border border-purple-100 shadow-sm">
            <span class="font-mono font-black text-xs">TEL</span>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Prendas Confeccionadas</p>
            <p class="text-2xl font-black text-emerald-600 mt-1 font-mono">{{ kpis.prendas }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center border border-emerald-100 shadow-sm">
            <span class="font-mono font-black text-xs">PRE</span>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Bajo Stock Mínimo</p>
            <p class="text-2xl font-black text-rose-600 mt-1 font-mono">{{ kpis.alertasStock }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-rose-50 text-rose-600 flex items-center justify-center border border-rose-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
          </div>
        </div>
      </div>

      <!-- Filtros y Búsqueda -->
      <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 flex flex-col md:flex-row md:items-center gap-3">
        <div class="relative flex-1">
          <input
            v-model="filtroBusqueda"
            type="text"
            placeholder="Buscar por código (ej. TEL-001), nombre de tela, talla o color..."
            class="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
          />
          <svg class="w-4 h-4 text-slate-400 absolute left-3.5 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <select
            v-model="filtroTipo"
            class="px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-700 outline-none focus:bg-white focus:border-purple-500"
          >
            <option value="">Todas las clasificaciones</option>
            <option value="MateriaPrima">Materia Prima</option>
            <option value="Insumo">Insumo</option>
            <option value="PrendaTerminada">Prenda Terminada</option>
            <option value="PiezaCorte">Pieza de Corte</option>
          </select>

          <label class="flex items-center gap-2 text-xs font-bold text-slate-600 bg-slate-50 px-3 py-2 rounded-xl border border-slate-200 cursor-pointer select-none">
            <input type="checkbox" v-model="mostrarInactivos" class="rounded text-purple-600 focus:ring-purple-500" />
            <span>Mostrar inactivos</span>
          </label>
        </div>
      </div>

      <!-- Tabla de Datos -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-100">
          <h3 class="text-sm font-black text-slate-800">Listado Maestro de Inventario</h3>
          <span v-if="!isLoading" class="text-xs font-semibold text-slate-500">
            {{ articulosFiltrados.length }} {{ articulosFiltrados.length === 1 ? 'registro' : 'registros' }}
          </span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="bg-slate-50/70 border-b border-slate-100 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                <th class="py-3.5 px-6">Código / Artículo</th>
                <th class="py-3.5 px-4">Clasificación</th>
                <th class="py-3.5 px-4">U. Medida</th>
                <th class="py-3.5 px-4">Variantes</th>
                <th class="py-3.5 px-4 text-right">Stock Global</th>
                <th class="py-3.5 px-4 text-right">Stock Mínimo</th>
                <th class="py-3.5 px-4 text-right">Costo Promedio</th>
                <th class="py-3.5 px-4 text-center">Estado</th>
                <th class="py-3.5 px-6 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-if="isLoading">
                <td colspan="9" class="py-14 text-center text-slate-400 font-semibold">
                  Cargando artículos...
                </td>
              </tr>

              <tr
                v-for="item in articulosFiltrados"
                :key="item.idArticulo"
                :class="['hover:bg-purple-50/20 transition-colors', !item.estadoArticulo ? 'opacity-60 bg-slate-50/40' : '']"
              >
                <td class="py-3.5 px-6 min-w-[200px]">
                  <span class="block text-slate-900 font-bold text-[13px] leading-tight">{{ item.nombreArticulo }}</span>
                  <span class="inline-block mt-1 px-1.5 py-0.5 rounded-md bg-purple-50 text-[11px] text-purple-700 font-mono font-bold border border-purple-200">
                    {{ item.codigoArticulo }}
                  </span>
                </td>

                <td class="py-3.5 px-4">
                  <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[10px] font-bold border whitespace-nowrap', getTipoBadgeClass(item.tipoArticulo)]">
                    <span :class="['w-1.5 h-1.5 rounded-full', getTipoDotClass(item.tipoArticulo)]"></span>
                    {{ getTipoLabel(item.tipoArticulo) }}
                  </span>
                </td>

                <td class="py-3.5 px-4 text-slate-800 font-semibold">
                  {{ item.nombreMedida }}
                  <span class="block text-[10px] text-slate-400 font-mono">({{ item.codigoMedida }})</span>
                </td>

                <td class="py-3.5 px-4">
                  <div v-if="item.tipoArticulo === 'PrendaTerminada' && (item.talla || item.color)" class="flex flex-wrap items-center gap-1">
                    <span v-if="item.talla" class="px-2 py-0.5 rounded-md bg-indigo-50 border border-indigo-200 text-indigo-700 text-[10px] font-bold">
                      Talla {{ item.talla }}
                    </span>
                    <span v-if="item.color" class="px-2 py-0.5 rounded-md bg-purple-50 border border-purple-200 text-purple-700 text-[10px] font-bold">
                      {{ item.color }}
                    </span>
                  </div>
                  <span v-else class="text-slate-300 font-mono text-xs">—</span>
                </td>

                <td class="py-3.5 px-4 text-right">
                  <button
                    @click="verDesgloseBodegas(item)"
                    :class="[
                      'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg border font-mono font-bold text-xs transition shadow-2xs',
                      item.stockTotal <= item.stockMinimo && item.estadoArticulo
                        ? 'bg-rose-50 text-rose-700 border-rose-300 hover:bg-rose-100 font-black'
                        : 'bg-white text-slate-800 border-slate-200 hover:border-purple-300 hover:text-purple-700 hover:bg-purple-50'
                    ]"
                    :title="'Ver existencias en bodegas'"
                  >
                    <span v-if="item.stockTotal <= item.stockMinimo && item.estadoArticulo" class="w-1.5 h-1.5 rounded-full bg-rose-500 animate-pulse"></span>
                    {{ item.stockTotal.toFixed(2) }}
                  </button>
                </td>

                <td class="py-3.5 px-4 text-right font-mono text-slate-500 font-semibold">
                  {{ item.stockMinimo.toFixed(2) }}
                </td>

                <td class="py-3.5 px-4 text-right font-mono font-bold text-slate-800 whitespace-nowrap">
                  {{ formatMoneda(item.costoPromedio) }}
                </td>

                <td class="py-3.5 px-4 text-center">
                  <span :class="[
                    'px-2.5 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-2xs',
                    item.estadoArticulo ? 'bg-emerald-50 text-emerald-700 border-emerald-300' : 'bg-rose-50 text-rose-700 border-rose-300'
                  ]">
                    {{ item.estadoArticulo ? 'Activo' : 'Inactivo' }}
                  </span>
                </td>

                <td class="py-3.5 px-6 text-right">
                  <div class="flex items-center justify-end gap-1.5">
                    <button
                      v-if="can('ARTICULOS', 'modificar')"
                      @click="abrirModalEditar(item)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-slate-700 bg-slate-100 hover:bg-purple-600 hover:text-white border border-slate-200 transition-all shadow-xs"
                    >
                      Editar
                    </button>
                    <button
                      v-if="can('ARTICULOS', 'eliminar')"
                      @click="solicitarToggleEstado(item)"
                      :class="[
                        'px-2.5 py-1.5 rounded-lg text-xs font-bold transition-all border shadow-xs',
                        item.estadoArticulo
                          ? 'bg-rose-50 hover:bg-rose-600 text-rose-700 hover:text-white border-rose-200'
                          : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-700 hover:text-white border-emerald-200'
                      ]"
                    >
                      {{ item.estadoArticulo ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>

              <tr v-if="articulosFiltrados.length === 0 && !isLoading">
                <td colspan="9" class="py-14 text-center">
                  <div class="w-14 h-14 bg-purple-50 text-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-3 border border-purple-100">
                    <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" /></svg>
                  </div>
                  <p class="text-sm font-black text-slate-800">No se encontraron artículos registrados</p>
                  <p class="text-xs text-slate-500 font-medium mt-1">Ajusta los filtros o registra un nuevo insumo/tela.</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Formulario de Creación / Edición -->
    <Transition name="fade">
      <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-3xl shadow-2xl w-full max-w-lg border border-slate-100 max-h-[92vh] flex flex-col overflow-hidden animate-scale-in">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <!-- Cabecera -->
          <div class="px-7 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">
                {{ isEditing ? 'Editar Especificaciones de Artículo' : 'Nuevo Artículo / Tela' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Catálogo maestro de inventario para maquila y confección</p>
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

              <!-- Clasificación y Código con Generador Automático -->
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
                <div>
                  <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                    Clasificación <span class="text-rose-500">*</span>
                  </label>
                  <select
                    v-model="formArticulo.tipoArticulo"
                    @change="onTipoArticuloChange"
                    class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                  >
                    <option value="MateriaPrima">Materia Prima (Telas)</option>
                    <option value="Insumo">Insumo (Avíos/Hilos)</option>
                    <option value="PrendaTerminada">Prenda Terminada</option>
                    <option value="PiezaCorte">Pieza de Corte</option>
                  </select>
                </div>

                <div>
                  <div class="flex items-center justify-between mb-1.5">
                    <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider">
                      Código Único <span class="text-rose-500">*</span>
                    </label>
                    <button
                      type="button"
                      @click="aplicarCodigoSugerido"
                      class="text-[10px] font-bold text-purple-700 hover:text-purple-900 underline inline-flex items-center gap-1"
                      title="Generar correlativo sugerido"
                    >
                      <span>Sugerir código</span>
                    </button>
                  </div>
                  <div class="relative">
                    <input
                      v-model="formArticulo.codigoArticulo"
                      @keypress="allowOnly.codigoInput($event)"
                      required
                      maxlength="30"
                      type="text"
                      placeholder="TEL-001"
                      class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 uppercase"
                    />
                  </div>
                  <p v-if="erroresForm.codigoArticulo" class="text-rose-600 text-[11px] font-bold mt-1">
                    {{ erroresForm.codigoArticulo }}
                  </p>
                </div>
              </div>

              <!-- Nombre del Artículo -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Nombre del Artículo / Tela <span class="text-rose-500">*</span>
                </label>
                <input
                  v-model="formArticulo.nombreArticulo"
                  @keypress="allowOnly.nombreInput($event)"
                  required
                  maxlength="150"
                  type="text"
                  placeholder="Ej: Felpa Algodón 24/1 o Playera Polo Piqué"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                />
                <p v-if="erroresForm.nombreArticulo" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.nombreArticulo }}
                </p>
              </div>

              <!-- Unidad de Medida Base -->
              <div>
                <div class="flex items-center justify-between mb-1.5">
                  <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider">
                    Unidad de Medida Base <span class="text-rose-500">*</span>
                  </label>
                  <span v-if="formArticulo.tipoArticulo === 'PrendaTerminada'" class="text-[10px] text-emerald-600 font-bold">
                    ✓ Preseleccionado en Pieza
                  </span>
                </div>
                <select
                  v-model="formArticulo.idUnidadBaseMedida"
                  required
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                >
                  <option v-for="u in unidadesMedida" :key="u.idUnidadMedida" :value="u.idUnidadMedida">
                    {{ u.nombreMedida }} ({{ u.codigoMedida }}) · {{ u.tipoMedida }}
                  </option>
                </select>
                <p v-if="erroresForm.idUnidadBaseMedida" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.idUnidadBaseMedida }}
                </p>
              </div>

              <!-- Variantes para Prenda Terminada -->
              <div v-if="formArticulo.tipoArticulo === 'PrendaTerminada'" class="p-4 bg-purple-50/50 rounded-2xl border border-purple-100">
                <p class="text-xs font-black uppercase tracking-wider text-purple-800 mb-2.5 flex items-center gap-1.5">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" /></svg>
                  Variantes de Confección
                </p>
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[11px] font-bold text-slate-600 mb-1">Talla (Opcional)</label>
                    <input
                      v-model="formArticulo.talla"
                      type="text"
                      placeholder="M, L, XL, 32..."
                      maxlength="10"
                      class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:outline-none focus:border-purple-500"
                    />
                  </div>
                  <div>
                    <label class="block text-[11px] font-bold text-slate-600 mb-1">Color (Opcional)</label>
                    <input
                      v-model="formArticulo.color"
                      type="text"
                      placeholder="Negro, Azul Marino..."
                      maxlength="30"
                      class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-800 focus:outline-none focus:border-purple-500"
                    />
                  </div>
                </div>
              </div>

              <!-- Stock Mínimo y Costo Promedio -->
              <div class="grid grid-cols-2 gap-3.5">
                <div>
                  <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">Stock Mínimo</label>
                  <input
                    v-model.number="formArticulo.stockMinimo"
                    min="0"
                    step="0.01"
                    type="number"
                    placeholder="0.00"
                    class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                  />
                  <p class="text-[10px] text-slate-400 mt-1">Umbral para alertas de reabastecimiento.</p>
                </div>

                <div>
                  <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">Costo Referencial (Q)</label>
                  <input
                    v-model.number="formArticulo.costoPromedio"
                    min="0"
                    step="0.0001"
                    type="number"
                    placeholder="0.00"
                    class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono font-bold text-slate-900 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
                  />
                  <p class="text-[10px] text-slate-400 mt-1">Costo ponderado de compras o fabricación.</p>
                </div>
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
                :disabled="isSaving"
                class="px-5 py-2.5 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg transition-all disabled:opacity-60"
              >
                {{ isSaving ? 'Guardando...' : (isEditing ? 'Revisar y Guardar' : 'Registrar Artículo') }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Transition>

    <!-- Modal Desglose de Existencias por Almacén -->
    <Transition name="fade">
      <div v-if="showBodegasModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="bg-white rounded-3xl shadow-2xl w-full max-w-md border border-slate-100 overflow-hidden animate-scale-in">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <div class="flex items-start justify-between gap-4 px-6 pt-5 pb-4 border-b border-slate-100 bg-slate-50/50">
            <div class="flex items-center gap-3 min-w-0">
              <div class="w-10 h-10 rounded-xl bg-purple-100 text-purple-700 flex items-center justify-center flex-shrink-0 font-bold border border-purple-200">
                📦
              </div>
              <div class="min-w-0">
                <h3 class="text-sm font-black text-slate-800 leading-tight">Existencias por Bodega</h3>
                <p class="text-xs text-slate-500 truncate">{{ articuloSeleccionadoBodegas?.nombreArticulo }} · <span class="font-mono text-purple-700 font-bold">{{ articuloSeleccionadoBodegas?.codigoArticulo }}</span></p>
              </div>
            </div>
            <button @click="showBodegasModal = false" class="p-1 rounded-lg text-slate-400 hover:text-slate-700">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <div class="p-6 space-y-2.5 max-h-[50vh] overflow-y-auto">
            <div
              v-for="bodega in articuloSeleccionadoBodegas?.desgloseBodegas"
              :key="bodega.idAlmacen"
              class="flex items-center justify-between p-3.5 bg-slate-50 border border-slate-200 rounded-xl"
            >
              <span class="text-xs font-bold text-slate-700">{{ bodega.nombreAlmacen }}</span>
              <span class="text-xs font-mono font-black text-purple-700">
                {{ Number(bodega.stockActual).toFixed(2) }} {{ articuloSeleccionadoBodegas?.codigoMedida }}
              </span>
            </div>

            <div v-if="!articuloSeleccionadoBodegas?.desgloseBodegas?.length" class="text-center py-8 text-xs text-slate-400 italic">
              Este artículo no cuenta con saldo físico en ninguna bodega registrada.
            </div>
          </div>

          <div class="px-6 py-4 bg-slate-50/80 border-t border-slate-100 flex justify-end">
            <button
              @click="showBodegasModal = false"
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