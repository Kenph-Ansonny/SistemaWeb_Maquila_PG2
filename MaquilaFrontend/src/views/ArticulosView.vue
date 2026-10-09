<script setup>
import { ref, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import articuloService from '../services/articuloService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

const articulos = ref([])
const unidadesMedida = ref([])
const isLoading = ref(true)
const showModal = ref(false)
const showBodegasModal = ref(false)
const articuloSeleccionadoBodegas = ref(null)

const isEditing = ref(false)
const currentId = ref(null)

const filtroBusqueda = ref('')
const filtroTipo = ref('')
const mostrarInactivos = ref(false)

// type: 'danger' | 'success' | 'primary' (primary = guardar/registrar)
// confirmText y details son opcionales
const confirmModal = ref({
  show: false,
  title: '',
  message: '',
  action: null,
  type: 'warning',
  confirmText: '',
  details: []
})

const alertModal = ref({
  show: false,
  title: '',
  message: '',
  type: 'success'
})

const formArticulo = ref({
  codigoArticulo: '',
  nombreArticulo: '',
  tipoArticulo: 'MateriaPrima',
  idUnidadBaseMedida: '',
  stockMinimo: 0,
  costoPromedio: 0,
  talla: '',
  color: ''
})

const showAlert = (title, message, type = 'success') => {
  alertModal.value = { show: true, title, message, type }
}

const formatMoneda = (val) => {
  return new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(val || 0)
}

const articulosFiltrados = computed(() => {
  return articulos.value.filter(a => {
    if (!mostrarInactivos.value && !a.estadoArticulo) return false
    if (filtroTipo.value && a.tipoArticulo !== filtroTipo.value) return false

    if (filtroBusqueda.value.trim()) {
      const term = filtroBusqueda.value.toLowerCase()
      return a.codigoArticulo.toLowerCase().includes(term) || a.nombreArticulo.toLowerCase().includes(term)
    }
    return true
  })
})

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
    articulos.value = resArticulos.data
    unidadesMedida.value = resUnidades.data
  } catch {
    showAlert('Error', 'No se pudieron sincronizar los artículos con el servidor.', 'error')
  } finally {
    isLoading.value = false
  }
}

const abrirModalCrear = () => {
  if (!can('ARTICULOS', 'insertar')) return
  isEditing.value = false
  currentId.value = null
  formArticulo.value = {
    codigoArticulo: '',
    nombreArticulo: '',
    tipoArticulo: 'MateriaPrima',
    idUnidadBaseMedida: unidadesMedida.value[0]?.idUnidadMedida || '',
    stockMinimo: 0,
    costoPromedio: 0,
    talla: '',
    color: ''
  }
  showModal.value = true
}

const abrirModalEditar = (item) => {
  if (!can('ARTICULOS', 'modificar')) return
  isEditing.value = true
  currentId.value = item.idArticulo
  formArticulo.value = {
    codigoArticulo: item.codigoArticulo,
    nombreArticulo: item.nombreArticulo,
    tipoArticulo: item.tipoArticulo,
    idUnidadBaseMedida: item.idUnidadBaseMedida,
    stockMinimo: item.stockMinimo,
    costoPromedio: item.costoPromedio,
    talla: item.talla || '',
    color: item.color || ''
  }
  showModal.value = true
}

const verDesgloseBodegas = (item) => {
  articuloSeleccionadoBodegas.value = item
  showBodegasModal.value = true
}

/**
 * Paso 1: valida el formulario y, si es correcto, pide confirmación
 * mostrando un resumen de lo que se va a guardar.
 * El formulario permanece abierto (con sus datos) hasta que se confirme.
 */
const guardarArticulo = () => {
  const codigo = formArticulo.value.codigoArticulo?.trim().toUpperCase()
  const nombre = formArticulo.value.nombreArticulo?.trim()

  if (!TextRules.esCodigoValido(codigo)) {
    showAlert('Código Inválido', 'El código debe contener entre 2 y 30 caracteres alfanuméricos y guiones (ej. TEL-001).', 'error')
    return
  }

  if (!TextRules.esNombreValido(nombre, 3, 150)) {
    showAlert('Nombre Inválido', 'El nombre del artículo debe tener entre 3 y 150 caracteres legibles sin caracteres especiales complejos.', 'error')
    return
  }

  const unidad = unidadesMedida.value.find(u => u.idUnidadMedida === formArticulo.value.idUnidadBaseMedida)
  const details = [
    { label: 'Código', value: codigo },
    { label: 'Nombre', value: nombre },
    { label: 'Clasificación', value: getTipoLabel(formArticulo.value.tipoArticulo) },
    { label: 'Unidad base', value: unidad ? `${unidad.nombreMedida} (${unidad.codigoMedida})` : '—' }
  ]

  if (formArticulo.value.tipoArticulo === 'PrendaTerminada') {
    if (formArticulo.value.talla?.trim()) details.push({ label: 'Talla', value: formArticulo.value.talla.trim() })
    if (formArticulo.value.color?.trim()) details.push({ label: 'Color', value: formArticulo.value.color.trim() })
  }

  details.push(
    { label: 'Stock mínimo', value: Number(formArticulo.value.stockMinimo || 0).toFixed(2) },
    { label: 'Costo referencial', value: formatMoneda(formArticulo.value.costoPromedio) }
  )

  confirmModal.value = {
    show: true,
    title: isEditing.value ? '¿Guardar los cambios?' : '¿Registrar el artículo?',
    message: isEditing.value
      ? 'Revisa los datos antes de actualizar el artículo en el catálogo.'
      : 'Revisa los datos antes de registrar el artículo en el catálogo.',
    details,
    confirmText: isEditing.value ? 'Sí, guardar cambios' : 'Sí, registrar',
    action: () => ejecutarGuardado(codigo, nombre),
    type: 'primary'
  }
}

// Paso 2: se ejecuta solo si el usuario confirma
const ejecutarGuardado = async (codigo, nombre) => {
  confirmModal.value.show = false

  try {
    const payload = {
      ...formArticulo.value,
      codigoArticulo: codigo,
      nombreArticulo: nombre
    }

    if (isEditing.value) {
      await articuloService.actualizar(currentId.value, payload)
      showAlert('Actualización Exitosa', 'El artículo ha sido modificado.')
    } else {
      await articuloService.crear(payload)
      showAlert('Registro Exitoso', 'El artículo ha sido registrado en el catálogo.')
    }
    showModal.value = false
    cargarDatos()
  } catch (err) {
    showAlert('Error al Procesar', err.response?.data?.message || 'Error en la solicitud.', 'error')
  }
}

const solicitarToggleEstado = (item) => {
  if (!can('ARTICULOS', 'eliminar')) return
  const accion = item.estadoArticulo ? 'desactivar' : 'activar'
  confirmModal.value = {
    show: true,
    title: '¿Confirmar cambio de estado?',
    message: `¿Deseas ${accion} el artículo "${item.nombreArticulo}" (${item.codigoArticulo})?`,
    details: [],
    confirmText: '',
    action: async () => {
      confirmModal.value.show = false
      try {
        const res = await articuloService.cambiarEstado(item.idArticulo)
        showAlert('Estado Actualizado', res.data.message)
        cargarDatos()
      } catch (err) {
        showAlert('Error', err.response?.data?.message || 'No se pudo cambiar el estado.', 'error')
      }
    },
    type: item.estadoArticulo ? 'danger' : 'success'
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

// Solo presentación: punto de color y etiqueta legible por tipo
const getTipoDotClass = (tipo) => {
  switch (tipo) {
    case 'MateriaPrima': return 'bg-purple-500'
    case 'Insumo': return 'bg-sky-500'
    case 'PrendaTerminada': return 'bg-emerald-500'
    case 'PiezaCorte': return 'bg-amber-500'
    default: return 'bg-slate-400'
  }
}

const getTipoLabel = (tipo) => {
  switch (tipo) {
    case 'MateriaPrima': return 'Materia prima'
    case 'Insumo': return 'Insumo'
    case 'PrendaTerminada': return 'Prenda terminada'
    case 'PiezaCorte': return 'Pieza de corte'
    default: return tipo
  }
}

onMounted(() => {
  cargarDatos()
})
</script>

<template>
  <MainLayout>
    <!-- Modal Notificación (Z-Index 70) -->
    <div v-if="alertModal.show" class="fixed inset-0 z-[70] flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-3xl p-7 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
        <div :class="['w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4 ring-8', alertModal.type === 'success' ? 'bg-emerald-50 text-emerald-600 ring-emerald-50/60' : 'bg-rose-50 text-rose-600 ring-rose-50/60']">
          <svg v-if="alertModal.type === 'success'" class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7" /></svg>
          <svg v-else class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M6 18L18 6M6 6l12 12" /></svg>
        </div>
        <h3 class="text-lg font-bold text-slate-800">{{ alertModal.title }}</h3>
        <p class="text-sm text-slate-500 mt-1.5 mb-6 leading-relaxed">{{ alertModal.message }}</p>
        <button
          @click="alertModal.show = false"
          class="w-full py-2.5 text-sm font-semibold text-white bg-gradient-to-tl from-purple-700 to-pink-500 hover:opacity-95 rounded-xl shadow-md shadow-purple-500/25 transition focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/30"
        >
          Aceptar
        </button>
      </div>
    </div>

    <!-- Modal Confirmación (Z-Index 60): cambio de estado y guardado de formulario -->
    <div v-if="confirmModal.show" class="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-3xl p-7 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in max-h-[92vh] overflow-y-auto">
        <div
          :class="[
            'w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4 ring-8',
            confirmModal.type === 'success'
              ? 'bg-emerald-50 text-emerald-600 ring-emerald-50/60'
              : confirmModal.type === 'primary'
                ? 'bg-purple-50 text-purple-600 ring-purple-50/60'
                : 'bg-rose-50 text-rose-600 ring-rose-50/60'
          ]"
        >
          <!-- Icono de pregunta (guardar / registrar) -->
          <svg v-if="confirmModal.type === 'primary'" class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          <!-- Icono de advertencia (cambio de estado) -->
          <svg v-else class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
        </div>
        <h3 class="text-lg font-bold text-slate-800">{{ confirmModal.title }}</h3>
        <p :class="['text-sm text-slate-500 mt-1.5 leading-relaxed', confirmModal.details?.length ? 'mb-4' : 'mb-6']">{{ confirmModal.message }}</p>

        <!-- Resumen de los datos a guardar -->
        <dl v-if="confirmModal.details?.length" class="text-left mb-6 p-3.5 bg-slate-50 border border-slate-200 rounded-xl space-y-2">
          <div v-for="d in confirmModal.details" :key="d.label" class="flex items-start justify-between gap-4 text-xs">
            <dt class="text-slate-500 font-medium flex-shrink-0">{{ d.label }}</dt>
            <dd class="text-slate-800 font-semibold text-right break-words min-w-0">{{ d.value }}</dd>
          </div>
        </dl>

        <div class="grid grid-cols-2 gap-3">
          <button
            @click="confirmModal.show = false"
            class="py-2.5 text-sm font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition focus:outline-none focus-visible:ring-4 focus-visible:ring-slate-300/50"
          >
            {{ confirmModal.type === 'primary' ? 'Volver a revisar' : 'Cancelar' }}
          </button>
          <button
            @click="confirmModal.action"
            :class="[
              'py-2.5 px-2 text-sm font-semibold text-white rounded-xl shadow-md transition focus:outline-none focus-visible:ring-4',
              confirmModal.type === 'success'
                ? 'bg-emerald-600 hover:bg-emerald-700 shadow-emerald-500/25 focus-visible:ring-emerald-500/30'
                : confirmModal.type === 'primary'
                  ? 'bg-gradient-to-tl from-purple-700 to-pink-500 hover:opacity-95 shadow-purple-500/25 focus-visible:ring-purple-500/30'
                  : 'bg-rose-600 hover:bg-rose-700 shadow-rose-500/25 focus-visible:ring-rose-500/30'
            ]"
          >
            {{ confirmModal.confirmText || 'Confirmar' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Sin Permiso -->
    <div v-if="!can('ARTICULOS', 'consultar')" class="bg-white rounded-3xl p-14 text-center shadow-md border border-slate-100">
      <div class="w-16 h-16 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4 ring-8 ring-rose-50/60">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>
      </div>
      <h3 class="text-base font-bold text-slate-800">Acceso restringido</h3>
      <p class="text-sm text-slate-500 mt-1.5 max-w-sm mx-auto">No cuentas con autorización para consultar el inventario de artículos.</p>
    </div>

    <!-- Contenido Principal -->
    <div v-else class="space-y-5">
      <!-- Filtros y Cabecera -->
      <div class="bg-white rounded-2xl p-6 shadow-md shadow-slate-200/60 border border-slate-100">
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-5 border-b border-slate-100 gap-4">
          <div class="flex items-center gap-4">
            <div class="w-12 h-12 rounded-2xl bg-gradient-to-tl from-purple-700 to-pink-500 text-white flex items-center justify-center shadow-lg shadow-purple-500/30 flex-shrink-0">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
              </svg>
            </div>
            <div>
              <h2 class="text-lg font-bold text-slate-800 leading-tight">Catálogo de artículos y telas</h2>
              <p class="text-sm text-slate-500 mt-0.5">Control de materias primas, insumos y prendas terminadas.</p>
            </div>
          </div>
          <button
            v-if="can('ARTICULOS', 'insertar')"
            @click="abrirModalCrear"
            class="px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-sm rounded-xl shadow-lg shadow-purple-500/30 hover:shadow-purple-500/40 hover:opacity-95 active:scale-[0.98] transition flex items-center gap-2 focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/30 whitespace-nowrap"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" /></svg>
            Nuevo artículo
          </button>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 gap-4 pt-5 items-end">
          <div class="sm:col-span-2">
            <label for="filtro-busqueda" class="block text-xs font-semibold text-slate-600 mb-1.5">Buscar por código o nombre</label>
            <div class="relative">
              <svg class="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
              <input
                id="filtro-busqueda"
                v-model="filtroBusqueda"
                type="text"
                placeholder="Ej: TEL-001 o Felpa Algodón..."
                class="w-full h-10 pl-10 pr-3 bg-white border border-slate-200 rounded-xl text-sm text-slate-800 placeholder:text-slate-400 shadow-xs outline-none transition hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15"
              />
            </div>
          </div>

          <div>
            <label for="filtro-tipo" class="block text-xs font-semibold text-slate-600 mb-1.5">Tipo de artículo</label>
            <div class="relative">
              <select
                id="filtro-tipo"
                v-model="filtroTipo"
                class="w-full h-10 pl-3 pr-9 appearance-none bg-white border border-slate-200 rounded-xl text-sm text-slate-700 shadow-xs outline-none transition cursor-pointer hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15"
              >
                <option value="">Todos los tipos</option>
                <option value="MateriaPrima">Materia Prima</option>
                <option value="Insumo">Insumo</option>
                <option value="PrendaTerminada">Prenda Terminada</option>
                <option value="PiezaCorte">Pieza de Corte</option>
              </select>
              <svg class="w-4 h-4 text-slate-400 absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 9l-7 7-7-7" />
              </svg>
            </div>
          </div>

          <!-- Switch (mismo v-model que el checkbox original) -->
          <div class="flex items-center h-10">
            <label class="flex items-center gap-3 text-sm font-medium text-slate-600 cursor-pointer select-none">
              <input type="checkbox" v-model="mostrarInactivos" class="sr-only peer" />
              <span class="relative w-10 h-6 rounded-full bg-slate-200 transition-colors peer-checked:bg-purple-600 peer-focus-visible:ring-4 peer-focus-visible:ring-purple-500/30 after:content-[''] after:absolute after:top-0.5 after:left-0.5 after:w-5 after:h-5 after:rounded-full after:bg-white after:shadow-sm after:transition-transform peer-checked:after:translate-x-4"></span>
              <span>Mostrar inactivos</span>
            </label>
          </div>
        </div>
      </div>

      <!-- Tabla de Datos -->
      <div class="bg-white rounded-2xl shadow-md shadow-slate-200/60 border border-slate-100 overflow-hidden">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-100">
          <h3 class="text-sm font-bold text-slate-800">Listado de artículos</h3>
          <span v-if="!isLoading" class="text-xs font-medium text-slate-500">
            {{ articulosFiltrados.length }} {{ articulosFiltrados.length === 1 ? 'resultado' : 'resultados' }}
          </span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[980px] text-left border-collapse">
            <thead>
              <tr class="bg-slate-50/80 border-b border-slate-100 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                <th class="py-3 px-6 whitespace-nowrap">Código / Nombre</th>
                <th class="py-3 px-4 whitespace-nowrap">Clasificación</th>
                <th class="py-3 px-4 whitespace-nowrap">U. Medida</th>
                <th class="py-3 px-4 whitespace-nowrap">Detalles extra</th>
                <th class="py-3 px-4 text-right whitespace-nowrap">Stock global</th>
                <th class="py-3 px-4 text-right whitespace-nowrap">Stock mínimo</th>
                <th class="py-3 px-4 text-right whitespace-nowrap">Costo promedio</th>
                <th class="py-3 px-4 text-center whitespace-nowrap">Estado</th>
                <th class="py-3 px-6 text-right whitespace-nowrap">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-[13px]">
              <!-- Skeleton de carga -->
              <template v-if="isLoading">
                <tr v-for="n in 5" :key="'sk-' + n" class="animate-pulse">
                  <td class="py-4 px-6">
                    <div class="h-3.5 w-40 bg-slate-200 rounded"></div>
                    <div class="h-2.5 w-16 bg-slate-100 rounded mt-2"></div>
                  </td>
                  <td class="py-4 px-4"><div class="h-6 w-28 bg-slate-100 rounded-lg"></div></td>
                  <td class="py-4 px-4"><div class="h-3.5 w-24 bg-slate-100 rounded"></div></td>
                  <td class="py-4 px-4"><div class="h-3.5 w-8 bg-slate-100 rounded"></div></td>
                  <td class="py-4 px-4"><div class="h-6 w-16 bg-slate-100 rounded-lg ml-auto"></div></td>
                  <td class="py-4 px-4"><div class="h-3.5 w-14 bg-slate-100 rounded ml-auto"></div></td>
                  <td class="py-4 px-4"><div class="h-3.5 w-16 bg-slate-100 rounded ml-auto"></div></td>
                  <td class="py-4 px-4"><div class="h-6 w-16 bg-slate-100 rounded-full mx-auto"></div></td>
                  <td class="py-4 px-6"><div class="h-7 w-32 bg-slate-100 rounded-lg ml-auto"></div></td>
                </tr>
              </template>

              <tr
                v-for="item in articulosFiltrados"
                :key="item.idArticulo"
                :class="['group hover:bg-purple-50/40 transition-colors', !item.estadoArticulo ? 'opacity-70' : '']"
              >
                <td class="py-4 px-6 min-w-[200px]">
                  <span class="block text-slate-900 font-semibold text-sm leading-snug">{{ item.nombreArticulo }}</span>
                  <span class="inline-block mt-1 px-1.5 py-0.5 rounded-md bg-purple-50 text-[11px] text-purple-700 font-mono font-semibold">{{ item.codigoArticulo }}</span>
                </td>
                <td class="py-4 px-4">
                  <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[11px] font-semibold border whitespace-nowrap', getTipoBadgeClass(item.tipoArticulo)]">
                    <span :class="['w-1.5 h-1.5 rounded-full', getTipoDotClass(item.tipoArticulo)]"></span>
                    {{ getTipoLabel(item.tipoArticulo) }}
                  </span>
                </td>
                <td class="py-4 px-4 text-slate-700 font-medium">
                  {{ item.nombreMedida }}
                  <span class="block text-[11px] text-slate-500 font-mono font-normal">({{ item.codigoMedida }})</span>
                </td>
                <!-- Detalles extra -->
                <td class="py-4 px-4">
                  <div v-if="item.tipoArticulo === 'PrendaTerminada' && (item.talla || item.color)" class="flex flex-col items-start gap-1.5">
                    <span v-if="item.talla" class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-indigo-50 border border-indigo-200/80 text-indigo-700 text-[11px] font-semibold">
                      <svg class="w-3 h-3 text-indigo-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
                      </svg>
                      Talla {{ item.talla }}
                    </span>
                    <span v-if="item.color" class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-purple-50 border border-purple-200/80 text-purple-700 text-[11px] font-semibold">
                      <span class="w-2 h-2 rounded-full bg-purple-500 border border-purple-400"></span>
                      {{ item.color }}
                    </span>
                  </div>
                  <span v-else class="text-slate-300 text-sm">—</span>
                </td>
                <td class="py-4 px-4 text-right">
                  <button
                    @click="verDesgloseBodegas(item)"
                    :class="[
                      'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg border font-bold font-mono text-xs tabular-nums transition focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/25',
                      item.stockTotal <= item.stockMinimo
                        ? 'bg-rose-50 text-rose-700 border-rose-200 hover:bg-rose-100'
                        : 'bg-white text-slate-800 border-slate-200 hover:border-purple-300 hover:text-purple-700 hover:bg-purple-50'
                    ]"
                    :title="item.stockTotal <= item.stockMinimo ? 'Stock bajo · Ver desglose por bodega' : 'Ver desglose por bodega'"
                  >
                    <span v-if="item.stockTotal <= item.stockMinimo" class="w-1.5 h-1.5 rounded-full bg-rose-500"></span>
                    {{ item.stockTotal.toFixed(2) }}
                  </button>
                </td>
                <td class="py-4 px-4 text-right font-mono tabular-nums text-slate-600 font-medium">
                  {{ item.stockMinimo.toFixed(2) }}
                </td>
                <td class="py-4 px-4 text-right font-mono tabular-nums font-semibold text-slate-800 whitespace-nowrap">
                  {{ formatMoneda(item.costoPromedio) }}
                </td>
                <td class="py-4 px-4 text-center">
                  <span :class="['inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold border', item.estadoArticulo ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-rose-50 text-rose-700 border-rose-200']">
                    <span :class="['w-1.5 h-1.5 rounded-full', item.estadoArticulo ? 'bg-emerald-500' : 'bg-rose-500']"></span>
                    {{ item.estadoArticulo ? 'Activo' : 'Inactivo' }}
                  </span>
                </td>
                <td class="py-4 px-6 text-right">
                  <div class="flex items-center justify-end gap-2">
                    <button
                      v-if="can('ARTICULOS', 'modificar')"
                      @click="abrirModalEditar(item)"
                      class="inline-flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-200 text-slate-600 hover:border-purple-300 hover:bg-purple-50 hover:text-purple-700 rounded-lg text-xs font-semibold transition focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/25"
                    >
                      <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" /></svg>
                      Editar
                    </button>
                    <button
                      v-if="can('ARTICULOS', 'eliminar')"
                      @click="solicitarToggleEstado(item)"
                      :class="[
                        'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition border focus:outline-none focus-visible:ring-4',
                        item.estadoArticulo
                          ? 'bg-white text-rose-600 border-rose-200 hover:bg-rose-600 hover:text-white hover:border-rose-600 focus-visible:ring-rose-500/25'
                          : 'bg-white text-emerald-600 border-emerald-200 hover:bg-emerald-600 hover:text-white hover:border-emerald-600 focus-visible:ring-emerald-500/25'
                      ]"
                    >
                      <svg v-if="item.estadoArticulo" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" /></svg>
                      <svg v-else class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
                      {{ item.estadoArticulo ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>

              <!-- Estado vacío -->
              <tr v-if="articulosFiltrados.length === 0 && !isLoading">
                <td colspan="9" class="py-14 text-center">
                  <div class="w-14 h-14 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto mb-3">
                    <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" /></svg>
                  </div>
                  <p class="text-sm font-semibold text-slate-700">Sin resultados</p>
                  <p class="text-sm text-slate-500 mt-0.5">No se encontraron artículos registrados con los filtros aplicados.</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Desglose de Existencias por Almacén -->
    <div v-if="showBodegasModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-3xl shadow-2xl w-full max-w-md border border-slate-100 animate-scale-in overflow-hidden">
        <div class="flex items-start justify-between gap-4 px-6 pt-6 pb-4">
          <div class="flex items-center gap-3 min-w-0">
            <div class="w-10 h-10 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center flex-shrink-0">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" /></svg>
            </div>
            <div class="min-w-0">
              <h3 class="text-base font-bold text-slate-800 leading-tight">Existencias por almacén</h3>
              <p class="text-xs text-slate-500 truncate">{{ articuloSeleccionadoBodegas?.nombreArticulo }} · <span class="font-mono">{{ articuloSeleccionadoBodegas?.codigoArticulo }}</span></p>
            </div>
          </div>
          <button
            @click="showBodegasModal = false"
            class="p-1.5 -mr-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition focus:outline-none focus-visible:ring-4 focus-visible:ring-slate-300/50"
            aria-label="Cerrar"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
        </div>

        <div class="px-6 pb-2 space-y-2 max-h-[50vh] overflow-y-auto">
          <div
            v-for="bodega in articuloSeleccionadoBodegas?.desgloseBodegas"
            :key="bodega.idAlmacen"
            class="flex items-center justify-between p-3.5 bg-slate-50 border border-slate-200 rounded-xl"
          >
            <span class="text-sm font-semibold text-slate-700">{{ bodega.nombreAlmacen }}</span>
            <span class="text-sm font-mono font-bold text-purple-700 tabular-nums">{{ bodega.stockActual.toFixed(2) }} {{ articuloSeleccionadoBodegas?.codigoMedida }}</span>
          </div>

          <div v-if="!articuloSeleccionadoBodegas?.desgloseBodegas?.length" class="text-center py-8">
            <div class="w-12 h-12 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto mb-2">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" /></svg>
            </div>
            <p class="text-sm text-slate-500 max-w-xs mx-auto">Este artículo aún no cuenta con movimientos o ingresos registrados en almacenes.</p>
          </div>
        </div>

        <div class="flex justify-end px-6 py-4 mt-2 bg-slate-50/70 border-t border-slate-100">
          <button
            @click="showBodegasModal = false"
            class="px-5 py-2 text-sm font-semibold text-white bg-gradient-to-tl from-purple-700 to-pink-500 hover:opacity-95 rounded-xl shadow-md shadow-purple-500/25 transition focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/30"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>

    <!-- Modal Formulario de Creación / Edición -->
    <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-3xl shadow-2xl w-full max-w-lg border border-slate-100 max-h-[92vh] flex flex-col animate-scale-in overflow-hidden">
        <!-- Cabecera -->
        <div class="flex items-start justify-between gap-4 px-7 pt-6 pb-4 border-b border-slate-100">
          <div class="flex items-center gap-3">
            <div class="w-11 h-11 rounded-xl bg-gradient-to-tl from-purple-700 to-pink-500 text-white flex items-center justify-center shadow-md shadow-purple-500/30 flex-shrink-0">
              <svg v-if="!isEditing" class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
              <svg v-else class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" /></svg>
            </div>
            <div>
              <h3 class="text-lg font-bold text-slate-800 leading-tight">{{ isEditing ? 'Editar artículo' : 'Nuevo artículo / tela' }}</h3>
              <p class="text-sm text-slate-500">Ingresa las especificaciones del catálogo maestro.</p>
            </div>
          </div>
          <button
            type="button"
            @click="showModal = false"
            class="p-1.5 -mr-2 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition focus:outline-none focus-visible:ring-4 focus-visible:ring-slate-300/50"
            aria-label="Cerrar"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
        </div>

        <form @submit.prevent="guardarArticulo" class="flex flex-col min-h-0 flex-1">
          <div class="px-7 py-5 space-y-5 overflow-y-auto">
            <!-- Identificación -->
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label for="f-codigo" class="block text-xs font-semibold text-slate-600 mb-1.5">Código único <span class="text-rose-500">*</span></label>
                <input id="f-codigo" v-model="formArticulo.codigoArticulo" @keypress="allowOnly.codigoInput($event)" required maxlength="30" type="text" placeholder="TEL-001" class="w-full h-10 px-3 bg-white border border-slate-200 rounded-xl text-sm text-slate-800 placeholder:text-slate-400 shadow-xs outline-none transition hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15 font-mono uppercase" />
                <p class="text-[11px] text-slate-500 mt-1">Letras, números y guiones.</p>
              </div>
              <div>
                <label for="f-tipo" class="block text-xs font-semibold text-slate-600 mb-1.5">Clasificación</label>
                <div class="relative">
                  <select id="f-tipo" v-model="formArticulo.tipoArticulo" class="w-full h-10 pl-3 pr-9 appearance-none bg-white border border-slate-200 rounded-xl text-sm text-slate-700 shadow-xs outline-none transition cursor-pointer hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15">
                    <option value="MateriaPrima">Materia Prima</option>
                    <option value="Insumo">Insumo</option>
                    <option value="PrendaTerminada">Prenda Terminada</option>
                    <option value="PiezaCorte">Pieza de Corte</option>
                  </select>
                  <svg class="w-4 h-4 text-slate-400 absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 9l-7 7-7-7" /></svg>
                </div>
              </div>
            </div>

            <div>
              <label for="f-nombre" class="block text-xs font-semibold text-slate-600 mb-1.5">Nombre del artículo <span class="text-rose-500">*</span></label>
              <input id="f-nombre" v-model="formArticulo.nombreArticulo" @keypress="allowOnly.nombreInput($event)" required maxlength="150" type="text" placeholder="Ej: Felpa Algodón 24/1" class="w-full h-10 px-3 bg-white border border-slate-200 rounded-xl text-sm text-slate-800 placeholder:text-slate-400 shadow-xs outline-none transition hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15" />
            </div>

            <div>
              <label for="f-unidad" class="block text-xs font-semibold text-slate-600 mb-1.5">Unidad de medida base <span class="text-rose-500">*</span></label>
              <div class="relative">
                <select id="f-unidad" v-model="formArticulo.idUnidadBaseMedida" required class="w-full h-10 pl-3 pr-9 appearance-none bg-white border border-slate-200 rounded-xl text-sm text-slate-700 shadow-xs outline-none transition cursor-pointer hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15">
                  <option v-for="u in unidadesMedida" :key="u.idUnidadMedida" :value="u.idUnidadMedida">
                    {{ u.nombreMedida }} ({{ u.codigoMedida }}) - {{ u.tipoMedida }}
                  </option>
                </select>
                <svg class="w-4 h-4 text-slate-400 absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 9l-7 7-7-7" /></svg>
              </div>
            </div>

            <!-- Variantes de Prenda Terminada -->
            <div v-if="formArticulo.tipoArticulo === 'PrendaTerminada'" class="p-4 bg-purple-50/50 rounded-2xl border border-purple-100">
              <p class="text-xs font-semibold text-purple-700 mb-3 flex items-center gap-1.5">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" /></svg>
                Variantes de la prenda
              </p>
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label for="f-talla" class="block text-xs font-semibold text-slate-600 mb-1.5">Talla <span class="font-normal text-slate-500">(opcional)</span></label>
                  <input id="f-talla" v-model="formArticulo.talla" type="text" placeholder="M, L, XL..." class="w-full h-10 px-3 bg-white border border-slate-200 rounded-xl text-sm text-slate-800 placeholder:text-slate-400 shadow-xs outline-none transition hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15" />
                </div>
                <div>
                  <label for="f-color" class="block text-xs font-semibold text-slate-600 mb-1.5">Color <span class="font-normal text-slate-500">(opcional)</span></label>
                  <input id="f-color" v-model="formArticulo.color" type="text" placeholder="Negro, Azul..." class="w-full h-10 px-3 bg-white border border-slate-200 rounded-xl text-sm text-slate-800 placeholder:text-slate-400 shadow-xs outline-none transition hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15" />
                </div>
              </div>
            </div>

            <!-- Inventario y costo -->
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label for="f-stock" class="block text-xs font-semibold text-slate-600 mb-1.5">Stock mínimo</label>
                <input id="f-stock" v-model.number="formArticulo.stockMinimo" min="0" step="0.01" type="number" class="w-full h-10 px-3 bg-white border border-slate-200 rounded-xl text-sm text-slate-800 shadow-xs outline-none transition tabular-nums hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15" />
                <p class="text-[11px] text-slate-500 mt-1">Se marca en rojo al llegar a este valor.</p>
              </div>
              <div>
                <label for="f-costo" class="block text-xs font-semibold text-slate-600 mb-1.5">Costo referencial</label>
                <div class="relative">
                  <span class="absolute left-3 top-1/2 -translate-y-1/2 text-sm font-semibold text-slate-400 pointer-events-none">Q</span>
                  <input id="f-costo" v-model.number="formArticulo.costoPromedio" min="0" step="0.0001" type="number" class="w-full h-10 pl-8 pr-3 bg-white border border-slate-200 rounded-xl text-sm text-slate-800 shadow-xs outline-none transition tabular-nums hover:border-slate-300 focus:border-purple-500 focus:ring-4 focus:ring-purple-500/15" />
                </div>
              </div>
            </div>
          </div>

          <!-- Pie -->
          <div class="flex justify-end gap-3 px-7 py-4 bg-slate-50/70 border-t border-slate-100">
            <button type="button" @click="showModal = false" class="px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-200/70 rounded-xl transition focus:outline-none focus-visible:ring-4 focus-visible:ring-slate-300/50">Cancelar</button>
            <button type="submit" class="px-5 py-2 text-sm font-semibold text-white bg-gradient-to-tl from-purple-700 to-pink-500 hover:opacity-95 rounded-xl shadow-md shadow-purple-500/25 transition focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/30">
              {{ isEditing ? 'Guardar cambios' : 'Registrar artículo' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </MainLayout>
</template>