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

const confirmModal = ref({
  show: false,
  title: '',
  message: '',
  action: null,
  type: 'warning'
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

const guardarArticulo = async () => {
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
    case 'Insumo': return 'bg-blue-50 text-blue-700 border-blue-200'
    case 'PrendaTerminada': return 'bg-emerald-50 text-emerald-700 border-emerald-200'
    case 'PiezaCorte': return 'bg-amber-50 text-amber-700 border-amber-200'
    default: return 'bg-slate-50 text-slate-700 border-slate-200'
  }
}

onMounted(() => {
  cargarDatos()
})
</script>

<template>
  <MainLayout>
    <!-- Modal Notificación (Z-Index 70) -->
    <div v-if="alertModal.show" class="fixed inset-0 z-[70] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
        <div :class="['w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-3', alertModal.type === 'success' ? 'bg-emerald-50 text-emerald-500' : 'bg-rose-50 text-rose-500']">
          <svg v-if="alertModal.type === 'success'" class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
          <svg v-else class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
        </div>
        <h3 class="text-base font-bold text-slate-800">{{ alertModal.title }}</h3>
        <p class="text-xs text-slate-500 mt-1 mb-5">{{ alertModal.message }}</p>
        <button @click="alertModal.show = false" class="w-full py-2.5 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">Aceptar</button>
      </div>
    </div>

    <!-- Modal Confirmación (Z-Index 60) -->
    <div v-if="confirmModal.show" class="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
        <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4">
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
        </div>
        <h3 class="text-base font-bold text-slate-800">{{ confirmModal.title }}</h3>
        <p class="text-xs text-slate-500 mt-2 mb-6">{{ confirmModal.message }}</p>
        <div class="flex justify-center gap-3">
          <button @click="confirmModal.show = false" class="px-4 py-2 text-xs font-semibold text-slate-500 bg-slate-100 hover:bg-slate-200 rounded-xl transition">Cancelar</button>
          <button @click="confirmModal.action" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">Confirmar</button>
        </div>
      </div>
    </div>

    <!-- Sin Permiso -->
    <div v-if="!can('ARTICULOS', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar el inventario de artículos.</p>
    </div>

    <!-- Contenido Principal -->
    <div v-else class="space-y-4">
      <!-- Filtros y Cabecera -->
      <div class="bg-white rounded-2xl p-5 shadow-md border border-slate-100">
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-4 border-b border-slate-100 gap-2">
          <div>
            <h2 class="text-base font-bold text-slate-800">Catálogo de Artículos y Telas</h2>
            <p class="text-xs text-slate-400 mt-0.5">Control de materias primas, insumos y prendas terminadas.</p>
          </div>
          <button 
            v-if="can('ARTICULOS', 'insertar')"
            @click="abrirModalCrear"
            class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-2"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
            Nuevo Artículo
          </button>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 gap-3 pt-4 items-center">
          <div class="sm:col-span-2">
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Buscar por Código o Nombre</label>
            <input 
              v-model="filtroBusqueda" 
              type="text" 
              placeholder="Ej: TEL-001 o Felpa Algodón..." 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500"
            />
          </div>

          <div>
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Tipo de Artículo</label>
            <select 
              v-model="filtroTipo" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700"
            >
              <option value="">Todos los tipos</option>
              <option value="MateriaPrima">Materia Prima</option>
              <option value="Insumo">Insumo</option>
              <option value="PrendaTerminada">Prenda Terminada</option>
              <option value="PiezaCorte">Pieza de Corte</option>
            </select>
          </div>

          <div class="flex items-center pt-5 sm:pt-4">
            <label class="flex items-center gap-2 text-xs font-semibold text-slate-600 cursor-pointer select-none">
              <input 
                type="checkbox" 
                v-model="mostrarInactivos" 
                class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500"
              />
              <span>Mostrar inactivos</span>
            </label>
          </div>
        </div>
      </div>

      <!-- Tabla de Datos -->
      <div class="bg-white rounded-2xl p-6 shadow-md border border-slate-100">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 text-[11px] font-bold text-slate-400 uppercase tracking-wider">
                <th class="py-3 px-4">Código / Nombre</th>
                <th class="py-3 px-4">Clasificación</th>
                <th class="py-3 px-4">U. Medida</th>
                <th class="py-3 px-4">Detalles Extra</th>
                <th class="py-3 px-4 text-right">Stock Global</th>
                <th class="py-3 px-4 text-right">Stock Mínimo</th>
                <th class="py-3 px-4 text-right">Costo Promedio</th>
                <th class="py-3 px-4 text-center">Estado</th>
                <th class="py-3 px-4 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="item in articulosFiltrados" :key="item.idArticulo" class="hover:bg-slate-50/60 transition">
                <td class="py-3.5 px-4 font-semibold text-slate-800">
                  <span class="block text-slate-900 font-bold">{{ item.nombreArticulo }}</span>
                  <span class="text-[10px] text-purple-700 font-mono font-bold">{{ item.codigoArticulo }}</span>
                </td>
                <td class="py-3.5 px-4">
                  <span :class="['px-2.5 py-1 rounded-lg text-[10px] font-bold border shadow-2xs', getTipoBadgeClass(item.tipoArticulo)]">
                    {{ item.tipoArticulo }}
                  </span>
                </td>
                <td class="py-3.5 px-4 text-slate-700 font-medium">
                  {{ item.nombreMedida }} <span class="text-[11px] text-slate-400 font-mono">({{ item.codigoMedida }})</span>
                </td>
                <!-- Columna Detalles Extra Optimizada -->
                <td class="py-3.5 px-4">
                  <div v-if="item.tipoArticulo === 'PrendaTerminada' && (item.talla || item.color)" class="flex flex-wrap items-center gap-1.5">
                    <span v-if="item.talla" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-indigo-50 border border-indigo-200/80 text-indigo-700 text-[10px] font-bold shadow-2xs">
                      <svg class="w-3 h-3 text-indigo-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
                      </svg>
                      {{ item.talla }}
                    </span>
                    <span v-if="item.color" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-purple-50 border border-purple-200/80 text-purple-700 text-[10px] font-bold shadow-2xs">
                      <span class="w-2 h-2 rounded-full bg-purple-500 border border-purple-400"></span>
                      {{ item.color }}
                    </span>
                  </div>
                  <span v-else class="text-slate-400 text-xs font-mono">—</span>
                </td>
                <td class="py-3.5 px-4 text-right">
                  <button 
                    @click="verDesgloseBodegas(item)"
                    class="font-bold font-mono text-xs underline decoration-dotted hover:text-purple-600 transition"
                    :class="item.stockTotal <= item.stockMinimo ? 'text-rose-600' : 'text-slate-900'"
                    title="Ver desglose por bodega"
                  >
                    {{ item.stockTotal.toFixed(2) }}
                  </button>
                </td>
                <!-- Columna Stock Mínimo Optimizada -->
                <td class="py-3.5 px-4 text-right">
                  <span class="inline-block px-2.5 py-0.5 bg-slate-100 border border-slate-200/90 rounded-md text-slate-800 font-mono font-semibold text-xs shadow-2xs">
                    {{ item.stockMinimo.toFixed(2) }}
                  </span>
                </td>
                <td class="py-3.5 px-4 text-right font-mono font-medium text-slate-700">
                  {{ formatMoneda(item.costoPromedio) }}
                </td>
                <td class="py-3.5 px-4 text-center">
                  <span :class="['px-2.5 py-0.5 rounded-full text-[10px] font-bold', item.estadoArticulo ? 'bg-emerald-50 text-emerald-600 border border-emerald-200' : 'bg-rose-50 text-rose-600 border border-rose-200']">
                    {{ item.estadoArticulo ? 'Activo' : 'Inactivo' }}
                  </span>
                </td>
                <td class="py-3.5 px-4 text-right">
                  <div class="flex items-center justify-end gap-1.5">
                    <button 
                      v-if="can('ARTICULOS', 'modificar')"
                      @click="abrirModalEditar(item)"
                      class="px-2.5 py-1 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition shadow-2xs"
                    >
                      Editar
                    </button>
                    <button 
                      v-if="can('ARTICULOS', 'eliminar')"
                      @click="solicitarToggleEstado(item)"
                      :class="[
                        'px-2.5 py-1 rounded-lg text-xs font-semibold transition border shadow-2xs',
                        item.estadoArticulo ? 'bg-rose-50 hover:bg-rose-600 text-rose-600 hover:text-white border-rose-200' : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-600 hover:text-white border-emerald-200'
                      ]"
                    >
                      {{ item.estadoArticulo ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>
              <tr v-if="articulosFiltrados.length === 0 && !isLoading">
                <td colspan="9" class="py-8 text-center text-slate-400 text-xs italic">
                  No se encontraron artículos registrados con los filtros aplicados.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Desglose de Existencias por Almacén -->
    <div v-if="showBodegasModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-md border border-slate-100 animate-scale-in">
        <div class="flex items-center justify-between pb-3 border-b border-slate-100">
          <div>
            <h3 class="text-sm font-bold text-slate-800">Existencias por Almacén</h3>
            <p class="text-[11px] text-slate-400">{{ articuloSeleccionadoBodegas?.nombreArticulo }} ({{ articuloSeleccionadoBodegas?.codigoArticulo }})</p>
          </div>
          <button @click="showBodegasModal = false" class="text-slate-400 hover:text-slate-600 text-base">✕</button>
        </div>

        <div class="my-4 space-y-2">
          <div 
            v-for="bodega in articuloSeleccionadoBodegas?.desgloseBodegas" 
            :key="bodega.idAlmacen"
            class="flex items-center justify-between p-3 bg-slate-50 border border-slate-200 rounded-xl"
          >
            <span class="text-xs font-semibold text-slate-700">{{ bodega.nombreAlmacen }}</span>
            <span class="text-xs font-mono font-bold text-purple-700">{{ bodega.stockActual.toFixed(2) }} {{ articuloSeleccionadoBodegas?.codigoMedida }}</span>
          </div>

          <div v-if="!articuloSeleccionadoBodegas?.desgloseBodegas?.length" class="text-center py-4 text-xs text-slate-400 italic">
            Este artículo aún no cuenta con movimientos o ingresos registrados en almacenes.
          </div>
        </div>

        <div class="flex justify-end pt-3 border-t border-slate-100">
          <button @click="showBodegasModal = false" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
            Cerrar
          </button>
        </div>
      </div>
    </div>

    <!-- Modal Formulario de Creación / Edición -->
    <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-md border border-slate-100 max-h-[90vh] overflow-y-auto">
        <h3 class="text-base font-bold text-slate-800 mb-1">{{ isEditing ? 'Editar Artículo' : 'Nuevo Artículo / Tela' }}</h3>
        <p class="text-xs text-slate-400 mb-4">Ingresa las especificaciones del catálogo maestro.</p>

        <form @submit.prevent="guardarArticulo" class="space-y-3.5">
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Código Único</label>
              <input v-model="formArticulo.codigoArticulo" @keypress="allowOnly.codigoInput($event)" required maxlength="30" type="text"  placeholder="TEL-001" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 font-mono uppercase" />
            </div>
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Clasificación</label>
              <select v-model="formArticulo.tipoArticulo" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700">
                <option value="MateriaPrima">Materia Prima</option>
                <option value="Insumo">Insumo</option>
                <option value="PrendaTerminada">Prenda Terminada</option>
                <option value="PiezaCorte">Pieza de Corte</option>
              </select>
            </div>
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Nombre del Artículo</label>
            <input v-model="formArticulo.nombreArticulo" @keypress="allowOnly.nombreInput($event)" required maxlength="150" type="text" placeholder="Ej: Felpa Algodón 24/1" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Unidad de Medida Base</label>
            <select v-model="formArticulo.idUnidadBaseMedida" required class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700">
              <option v-for="u in unidadesMedida" :key="u.idUnidadMedida" :value="u.idUnidadMedida">
                {{ u.nombreMedida }} ({{ u.codigoMedida }}) - {{ u.tipoMedida }}
              </option>
            </select>
          </div>

          <!-- Variantes de Prenda Terminada -->
          <div v-if="formArticulo.tipoArticulo === 'PrendaTerminada'" class="grid grid-cols-2 gap-3 p-3 bg-slate-50 rounded-xl border border-slate-200">
            <div>
              <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Talla (Opcional)</label>
              <input v-model="formArticulo.talla" type="text" placeholder="M, L, XL..." class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
            </div>
            <div>
              <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Color (Opcional)</label>
              <input v-model="formArticulo.color" type="text" placeholder="Negro, Azul..." class="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Stock Mínimo</label>
              <input v-model.number="formArticulo.stockMinimo" min="0" step="0.01" type="number" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
            </div>
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Costo Referencial (Q)</label>
              <input v-model.number="formArticulo.costoPromedio" min="0" step="0.0001" type="number" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" />
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-100">
            <button type="button" @click="showModal = false" class="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl transition">Cancelar</button>
            <button type="submit" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
              {{ isEditing ? 'Guardar Cambios' : 'Registrar Artículo' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </MainLayout>
</template>