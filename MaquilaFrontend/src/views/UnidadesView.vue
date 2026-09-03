<script setup>
import { ref, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import unidadService from '../services/unidadService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

const activeTab = ref('unidades') // 'unidades' | 'conversiones'
const unidades = ref([])
const conversiones = ref([])
const isLoading = ref(true)

// Buscadores
const filtroUnidad = ref('')
const filtroTipo = ref('')
const filtroConversion = ref('')

// Modales Formulario
const showModalUnidad = ref(false)
const isEditingUnidad = ref(false)
const currentUnidadId = ref(null)

const showModalConversion = ref(false)
const isEditingConversion = ref(false)

// Modales Centrales
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

// Formularios Reactivos
const formUnidad = ref({
  codigoMedida: '',
  nombreMedida: '',
  tipoMedida: 'Longitud'
})

const formConversion = ref({
  idUnidadOrigen: '',
  idUnidadDestino: '',
  factorConversion: 1
})

const showAlert = (title, message, type = 'success') => {
  alertModal.value = { show: true, title, message, type }
}

// Filtros Computados
const unidadesFiltradas = computed(() => {
  return unidades.value.filter(u => {
    if (filtroTipo.value && u.tipoMedida !== filtroTipo.value) return false
    if (filtroUnidad.value.trim()) {
      const t = filtroUnidad.value.toLowerCase()
      return u.codigoMedida.toLowerCase().includes(t) || 
             u.nombreMedida.toLowerCase().includes(t) ||
             u.tipoMedida.toLowerCase().includes(t)
    }
    return true
  })
})

// Búsqueda profunda: Código, Nombre y Dimensión
const conversionesFiltradas = computed(() => {
  return conversiones.value.filter(c => {
    if (filtroConversion.value.trim()) {
      const t = filtroConversion.value.toLowerCase()
      return c.codigoOrigen.toLowerCase().includes(t) || 
             c.nombreOrigen.toLowerCase().includes(t) ||
             c.codigoDestino.toLowerCase().includes(t) ||
             c.nombreDestino.toLowerCase().includes(t) ||
             c.tipoMedida.toLowerCase().includes(t)
    }
    return true
  })
})

// Unidades disponibles para destino (misma dimensión física)
const unidadesDestinoCompatibles = computed(() => {
  if (!formConversion.value.idUnidadOrigen) return []
  const origen = unidades.value.find(u => u.idUnidadMedida === formConversion.value.idUnidadOrigen)
  if (!origen) return []
  return unidades.value.filter(u => u.tipoMedida === origen.tipoMedida && u.idUnidadMedida !== origen.idUnidadMedida)
})

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
    unidades.value = resU.data
    conversiones.value = resC.data
  } catch {
    showAlert('Error de Comunicación', 'No se pudieron sincronizar las unidades y factores con el servidor.', 'error')
  } finally {
    isLoading.value = false
  }
}

// CRUD Unidades
const abrirModalCrearUnidad = () => {
  if (!can('UNIDADES', 'insertar')) return
  isEditingUnidad.value = false
  currentUnidadId.value = null
  formUnidad.value = { codigoMedida: '', nombreMedida: '', tipoMedida: 'Longitud' }
  showModalUnidad.value = true
}

const abrirModalEditarUnidad = (u) => {
  if (!can('UNIDADES', 'modificar')) return
  isEditingUnidad.value = true
  currentUnidadId.value = u.idUnidadMedida
  formUnidad.value = { codigoMedida: u.codigoMedida, nombreMedida: u.nombreMedida, tipoMedida: u.tipoMedida }
  showModalUnidad.value = true
}

const guardarUnidad = async () => {
  const cod = formUnidad.value.codigoMedida.trim().toUpperCase()
  const nom = formUnidad.value.nombreMedida.trim()

  if (!TextRules.esCodigoValido(cod)) {
    showAlert('Código Inválido', 'El código de unidad debe tener entre 1 y 10 caracteres alfanuméricos.', 'error')
    return
  }
  if (!TextRules.esNombreValido(nom, 2, 50)) {
    showAlert('Nombre Inválido', 'El nombre de la unidad contiene caracteres no permitidos o longitud incorrecta.', 'error')
    return
  }

  try {
    const payload = { ...formUnidad.value, codigoMedida: cod, nombreMedida: nom }
    if (isEditingUnidad.value) {
      await unidadService.actualizarUnidad(currentUnidadId.value, payload)
      showAlert('Actualización Exitosa', 'Unidad de medida modificada.')
    } else {
      await unidadService.crearUnidad(payload)
      showAlert('Registro Exitoso', 'Nueva unidad de medida guardada.')
    }
    showModalUnidad.value = false
    cargarDatos()
  } catch (err) {
    showAlert('Error', err.response?.data?.message || 'Error al procesar la unidad.', 'error')
  }
}

// CRUD Conversiones
const abrirModalCrearConversion = () => {
  if (!can('UNIDADES', 'insertar')) return
  isEditingConversion.value = false
  formConversion.value = {
    idUnidadOrigen: unidades.value[0]?.idUnidadMedida || '',
    idUnidadDestino: '',
    factorConversion: 1
  }
  showModalConversion.value = true
}

const abrirModalEditarConversion = (c) => {
  if (!can('UNIDADES', 'modificar')) return
  isEditingConversion.value = true
  formConversion.value = {
    idUnidadOrigen: c.idUnidadOrigen,
    idUnidadDestino: c.idUnidadDestino,
    factorConversion: c.factorConversion
  }
  showModalConversion.value = true
}

const guardarConversion = async () => {
  if (!formConversion.value.idUnidadOrigen || !formConversion.value.idUnidadDestino) {
    showAlert('Selección Incompleta', 'Debes seleccionar tanto la unidad origen como la destino.', 'error')
    return
  }
  if (formConversion.value.factorConversion <= 0) {
    showAlert('Factor Inválido', 'El factor de conversión debe ser mayor a cero.', 'error')
    return
  }

  try {
    await unidadService.guardarConversion(formConversion.value)
    showAlert('Conversión Registrada', 'Se guardó el factor y se recalculó su equivalencia recíproca.')
    showModalConversion.value = false
    cargarDatos()
  } catch (err) {
    showAlert('Error de Validación', err.response?.data?.message || 'Error al guardar factor.', 'error')
  }
}

const solicitarEliminarConversion = (c) => {
  if (!can('UNIDADES', 'eliminar')) return
  confirmModal.value = {
    show: true,
    title: '¿Eliminar factor de conversión?',
    message: `¿Estás seguro de eliminar la equivalencia ${c.nombreOrigen} (${c.codigoOrigen}) ⇄ ${c.nombreDestino} (${c.codigoDestino})?`,
    action: async () => {
      confirmModal.value.show = false
      try {
        await unidadService.eliminarConversion(c.idUnidadOrigen, c.idUnidadDestino)
        showAlert('Eliminación Completa', 'La regla de conversión ha sido removida.')
        cargarDatos()
      } catch (err) {
        showAlert('Error', err.response?.data?.message || 'No se pudo eliminar la conversión.', 'error')
      }
    },
    type: 'danger'
  }
}

const getTipoBadgeClass = (tipo) => {
  switch (tipo) {
    case 'Longitud': return 'bg-purple-50 text-purple-700 border-purple-200'
    case 'Masa': return 'bg-blue-50 text-blue-700 border-blue-200'
    case 'Unidad': return 'bg-emerald-50 text-emerald-700 border-emerald-200'
    case 'Volumen': return 'bg-amber-50 text-amber-700 border-amber-200'
    default: return 'bg-slate-50 text-slate-700 border-slate-200'
  }
}

onMounted(() => {
  cargarDatos()
})
</script>

<template>
  <MainLayout>
    <!-- Modal Central Notificación (Z-Index 70) -->
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

    <!-- Modal Central Confirmación (Z-Index 60) -->
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
    <div v-if="!can('UNIDADES', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar unidades de medida y factores de conversión.</p>
    </div>

    <!-- Contenido Principal -->
    <div v-else class="space-y-4">
      <!-- Selector de Pestañas y Botones -->
      <div class="bg-white rounded-2xl p-5 shadow-md border border-slate-100">
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-4 border-b border-slate-100 gap-3">
          <div>
            <h2 class="text-base font-bold text-slate-800">Unidades de Medida y Conversiones</h2>
            <p class="text-xs text-slate-400 mt-0.5">Control de dimensiones base y factores automáticos de transformación para maquila.</p>
          </div>
          
          <div class="flex items-center gap-2">
            <button 
              v-if="activeTab === 'unidades' && can('UNIDADES', 'insertar')"
              @click="abrirModalCrearUnidad"
              class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-1.5"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
              Nueva Unidad
            </button>
            <button 
              v-if="activeTab === 'conversiones' && can('UNIDADES', 'insertar')"
              @click="abrirModalCrearConversion"
              class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-1.5"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
              Nueva Conversión
            </button>
          </div>
        </div>

        <!-- Botones de Pestañas -->
        <div class="flex items-center gap-2 pt-4">
          <button 
            @click="activeTab = 'unidades'"
            :class="[
              'px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2',
              activeTab === 'unidades' ? 'bg-purple-600 text-white shadow-sm' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            ]"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" /></svg>
            Catálogo de Unidades ({{ unidades.length }})
          </button>
          <button 
            @click="activeTab = 'conversiones'"
            :class="[
              'px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2',
              activeTab === 'conversiones' ? 'bg-purple-600 text-white shadow-sm' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            ]"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" /></svg>
            Factores de Conversión ({{ conversiones.length }})
          </button>
        </div>
      </div>

      <!-- ========================================== -->
      <!-- PESTAÑA 1: CATÁLOGO DE UNIDADES            -->
      <!-- ========================================== -->
      <div v-if="activeTab === 'unidades'" class="space-y-4">
        <!-- Filtros Unidades -->
        <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 grid grid-cols-1 sm:grid-cols-3 gap-3">
          <div class="sm:col-span-2">
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Buscar Unidad o Código</label>
            <input 
              v-model="filtroUnidad" 
              type="text" 
              placeholder="Ej: YD, Yarda, Kilogramo..." 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" 
            />
          </div>
          <div>
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Tipo / Dimensión</label>
            <select 
              v-model="filtroTipo" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700 font-semibold"
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
        <div class="bg-white rounded-2xl p-6 shadow-md border border-slate-100">
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="border-b border-slate-100 text-[11px] font-bold text-slate-400 uppercase tracking-wider">
                  <th class="py-3 px-4">Código</th>
                  <th class="py-3 px-4">Nombre de la Unidad</th>
                  <th class="py-3 px-4">Dimensión Física</th>
                  <th class="py-3 px-4 text-center">Artículos Vinculados</th>
                  <th class="py-3 px-4 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-for="u in unidadesFiltradas" :key="u.idUnidadMedida" class="hover:bg-slate-50/60 transition">
                  <td class="py-3.5 px-4 font-mono font-bold text-purple-700 text-xs">
                    {{ u.codigoMedida }}
                  </td>
                  <td class="py-3.5 px-4 font-bold text-slate-800">
                    {{ u.nombreMedida }}
                  </td>
                  <td class="py-3.5 px-4">
                    <span :class="['px-2.5 py-0.5 rounded-md text-[10px] font-bold border', getTipoBadgeClass(u.tipoMedida)]">
                      {{ u.tipoMedida }}
                    </span>
                  </td>
                  <td class="py-3.5 px-4 text-center">
                    <span class="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-slate-100 text-slate-700 border border-slate-200">
                      {{ u.totalArticulosAsociados }} artículo(s)
                    </span>
                  </td>
                  <td class="py-3.5 px-4 text-right">
                    <button 
                      v-if="can('UNIDADES', 'modificar')"
                      @click="abrirModalEditarUnidad(u)"
                      class="px-2.5 py-1 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition shadow-2xs"
                    >
                      Editar
                    </button>
                  </td>
                </tr>
                <tr v-if="unidadesFiltradas.length === 0 && !isLoading">
                  <td colspan="5" class="py-8 text-center text-slate-400 text-xs italic">
                    No se encontraron unidades registradas con los filtros aplicados.
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
          <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Buscar por Unidad, Código o Dimensión</label>
          <input 
            v-model="filtroConversion" 
            type="text" 
            placeholder="Ej: YD, Metro, Yarda, Kilogramo, Longitud..." 
            class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" 
          />
        </div>

        <!-- Tabla Conversiones -->
        <div class="bg-white rounded-2xl p-6 shadow-md border border-slate-100">
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="border-b border-slate-100 text-[11px] font-bold text-slate-400 uppercase tracking-wider">
                  <th class="py-3 px-4">Unidad Base (1.00)</th>
                  <th class="py-3 px-4 text-center">Operación</th>
                  <th class="py-3 px-4">Unidad Destino</th>
                  <th class="py-3 px-4">Dimensión</th>
                  <th class="py-3 px-4 text-right">Factor Multiplicador</th>
                  <th class="py-3 px-4 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-for="c in conversionesFiltradas" :key="`${c.idUnidadOrigen}-${c.idUnidadDestino}`" class="hover:bg-slate-50/60 transition">
                  <td class="py-3.5 px-4 font-bold text-slate-800">
                    1.00 <span class="font-mono text-purple-700">({{ c.codigoOrigen }})</span> - {{ c.nombreOrigen }}
                  </td>
                  <td class="py-3.5 px-4 text-center text-slate-400 font-bold">
                    ➔
                  </td>
                  <td class="py-3.5 px-4 font-bold text-slate-800">
                    <span class="font-mono text-purple-700">({{ c.codigoDestino }})</span> {{ c.nombreDestino }}
                  </td>
                  <td class="py-3.5 px-4">
                    <span :class="['px-2.5 py-0.5 rounded-md text-[10px] font-bold border', getTipoBadgeClass(c.tipoMedida)]">
                      {{ c.tipoMedida }}
                    </span>
                  </td>
                  <td class="py-3.5 px-4 text-right font-mono font-bold text-slate-800">
                    {{ c.factorConversion.toFixed(6) }}
                  </td>
                  <td class="py-3.5 px-4 text-right">
                    <div class="flex items-center justify-end gap-1.5">
                      <!-- Botón Modificar Factor -->
                      <button 
                        v-if="can('UNIDADES', 'modificar')"
                        @click="abrirModalEditarConversion(c)"
                        class="px-2.5 py-1 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition shadow-2xs"
                        title="Modificar factor de conversión"
                      >
                        Editar
                      </button>
                      <!-- Botón Eliminar Factor -->
                      <button 
                        v-if="can('UNIDADES', 'eliminar')"
                        @click="solicitarEliminarConversion(c)"
                        class="px-2.5 py-1 bg-rose-50 hover:bg-rose-600 text-rose-600 hover:text-white rounded-lg text-xs font-semibold transition border border-rose-200 shadow-2xs"
                        title="Eliminar factor y su recíproco"
                      >
                        Eliminar
                      </button>
                    </div>
                  </td>
                </tr>
                <tr v-if="conversionesFiltradas.length === 0 && !isLoading">
                  <td colspan="6" class="py-8 text-center text-slate-400 text-xs italic">
                    No se encontraron factores de conversión que coincidan con la búsqueda.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Formulario Unidad -->
    <div v-if="showModalUnidad" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 animate-scale-in">
        <h3 class="text-base font-bold text-slate-800 mb-1">{{ isEditingUnidad ? 'Editar Unidad' : 'Nueva Unidad de Medida' }}</h3>
        <p class="text-xs text-slate-400 mb-4">Ingresa el símbolo y clasificación estándar.</p>

        <form @submit.prevent="guardarUnidad" class="space-y-3.5">
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Código (Símbolo)</label>
              <input 
                v-model="formUnidad.codigoMedida" 
                @keypress="allowOnly.codigoInput($event)"
                required 
                maxlength="10"
                type="text" 
                placeholder="YD" 
                class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 font-mono uppercase" 
              />
            </div>
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Dimensión</label>
              <select v-model="formUnidad.tipoMedida" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700 font-semibold">
                <option value="Longitud">Longitud</option>
                <option value="Masa">Masa</option>
                <option value="Unidad">Unidad</option>
                <option value="Volumen">Volumen</option>
              </select>
            </div>
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Nombre Completo</label>
            <input 
              v-model="formUnidad.nombreMedida" 
              @keypress="allowOnly.nombreInput($event)"
              required 
              maxlength="50"
              type="text" 
              placeholder="Yarda" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" 
            />
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-100">
            <button type="button" @click="showModalUnidad = false" class="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl transition">Cancelar</button>
            <button type="submit" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
              {{ isEditingUnidad ? 'Guardar Cambios' : 'Registrar Unidad' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Formulario Factor Conversión (Creación / Edición) -->
    <div v-if="showModalConversion" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-md border border-slate-100 animate-scale-in">
        <h3 class="text-base font-bold text-slate-800 mb-1">
          {{ isEditingConversion ? 'Modificar Factor de Conversión' : 'Nueva Regla de Conversión' }}
        </h3>
        <p class="text-xs text-slate-400 mb-4">El sistema recalculará automáticamente la relación inversa en la base de datos.</p>

        <form @submit.prevent="guardarConversion" class="space-y-3.5">
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Unidad Origen (1.00)</label>
              <select 
                v-model="formConversion.idUnidadOrigen" 
                :disabled="isEditingConversion"
                required 
                class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700 font-semibold disabled:opacity-60 disabled:cursor-not-allowed"
              >
                <option v-for="u in unidades" :key="u.idUnidadMedida" :value="u.idUnidadMedida">
                  {{ u.nombreMedida }} ({{ u.codigoMedida }}) - {{ u.tipoMedida }}
                </option>
              </select>
            </div>

            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Unidad Destino</label>
              <select 
                v-model="formConversion.idUnidadDestino" 
                :disabled="isEditingConversion"
                required 
                class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700 font-semibold disabled:opacity-60 disabled:cursor-not-allowed"
              >
                <option v-for="u in unidadesDestinoCompatibles" :key="u.idUnidadMedida" :value="u.idUnidadMedida">
                  {{ u.nombreMedida }} ({{ u.codigoMedida }})
                </option>
              </select>
            </div>
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Factor Multiplicador</label>
            <input 
              v-model.number="formConversion.factorConversion" 
              required 
              type="number" 
              step="0.000001" 
              min="0.000001" 
              placeholder="0.914400"
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 font-mono font-bold" 
            />
            <p class="text-[11px] text-slate-400 mt-1">
              Fórmula: 1 Unidad Origen = Factor × Unidad Destino
            </p>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-100">
            <button type="button" @click="showModalConversion = false" class="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl transition">Cancelar</button>
            <button type="submit" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
              {{ isEditingConversion ? 'Guardar Cambios' : 'Guardar Equivalencia' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </MainLayout>
</template>