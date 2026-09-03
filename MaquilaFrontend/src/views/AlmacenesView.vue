<script setup>
import { ref, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import almacenService from '../services/almacenService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

const almacenes = ref([])
const inventarioDetalle = ref([])
const almacenSeleccionado = ref(null)

const isLoading = ref(true)
const isLoadingDetalle = ref(false)
const showModal = ref(false)
const showDetalleModal = ref(false)
const isEditing = ref(false)
const currentId = ref(null)

const filtroBusqueda = ref('')
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

const formAlmacen = ref({
  nombreAlmacen: ''
})

const showAlert = (title, message, type = 'success') => {
  alertModal.value = { show: true, title, message, type }
}

const almacenesFiltrados = computed(() => {
  return almacenes.value.filter(a => {
    if (!mostrarInactivos.value && !a.estadoAlmacen) return false
    if (filtroBusqueda.value.trim()) {
      return a.nombreAlmacen.toLowerCase().includes(filtroBusqueda.value.toLowerCase())
    }
    return true
  })
})

const totalBodegasActivas = computed(() => almacenes.value.filter(a => a.estadoAlmacen).length)
const totalStockGlobal = computed(() => almacenes.value.reduce((acc, curr) => acc + curr.totalUnidadesStock, 0))

const cargarAlmacenes = async () => {
  if (!can('ALMACENES', 'consultar')) {
    isLoading.value = false
    return
  }

  isLoading.value = true
  try {
    const res = await almacenService.obtenerTodos()
    almacenes.value = res.data
  } catch {
    showAlert('Error', 'No se pudieron sincronizar los almacenes con el servidor.', 'error')
  } finally {
    isLoading.value = false
  }
}

const abrirModalCrear = () => {
  if (!can('ALMACENES', 'insertar')) return
  isEditing.value = false
  currentId.value = null
  formAlmacen.value = { nombreAlmacen: '' }
  showModal.value = true
}

const abrirModalEditar = (item) => {
  if (!can('ALMACENES', 'modificar')) return
  isEditing.value = true
  currentId.value = item.idAlmacen
  formAlmacen.value = { nombreAlmacen: item.nombreAlmacen }
  showModal.value = true
}

const verInventarioBodega = async (item) => {
  almacenSeleccionado.value = item
  isLoadingDetalle.value = true
  showDetalleModal.value = true
  try {
    const res = await almacenService.obtenerInventario(item.idAlmacen)
    inventarioDetalle.value = res.data
  } catch {
    showAlert('Error', 'No se pudo cargar el stock físico de este almacén.', 'error')
  } finally {
    isLoadingDetalle.value = false
  }
}

const guardarAlmacen = async () => {
  const nombre = formAlmacen.value.nombreAlmacen?.trim()

  if (!TextRules.esNombreValido(nombre, 3, 80)) {
    showAlert('Formato Inválido', 'El nombre del almacén debe tener entre 3 y 80 caracteres alfanuméricos válidos, sin símbolos extraños.', 'error')
    return
  }

  try {
    if (isEditing.value) {
      await almacenService.actualizar(currentId.value, { nombreAlmacen: nombre })
      showAlert('Actualización Exitosa', 'El almacén ha sido modificado.')
    } else {
      await almacenService.crear({ nombreAlmacen: nombre })
      showAlert('Registro Exitoso', 'El almacén ha sido registrado.')
    }
    showModal.value = false
    cargarAlmacenes()
  } catch (err) {
    showAlert('Error al Procesar', err.response?.data?.message || 'Error en la solicitud.', 'error')
  }
}

const solicitarToggleEstado = (item) => {
  if (!can('ALMACENES', 'eliminar')) return
  const accion = item.estadoAlmacen ? 'desactivar' : 'activar'
  confirmModal.value = {
    show: true,
    title: '¿Confirmar cambio de estado?',
    message: `¿Deseas ${accion} el almacén "${item.nombreAlmacen}"?`,
    action: async () => {
      confirmModal.value.show = false
      try {
        const res = await almacenService.cambiarEstado(item.idAlmacen)
        showAlert('Estado Actualizado', res.data.message)
        cargarAlmacenes()
      } catch (err) {
        showAlert('Error de Validación', err.response?.data?.message || 'No se pudo cambiar el estado.', 'error')
      }
    },
    type: item.estadoAlmacen ? 'danger' : 'success'
  }
}

onMounted(() => {
  cargarAlmacenes()
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
    <div v-if="!can('ALMACENES', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar la gestión de almacenes y bodegas.</p>
    </div>

    <!-- Contenido Principal -->
    <div v-else class="space-y-4">
      <!-- Tarjetas de Resumen Rápido -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Bodegas Operativas</p>
            <h4 class="text-xl font-bold text-slate-800 mt-0.5">{{ totalBodegasActivas }}</h4>
          </div>
          <div class="w-10 h-10 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center shadow-2xs">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-4 shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Unidades Totales en Custodia</p>
            <h4 class="text-xl font-bold text-slate-800 font-mono mt-0.5">{{ totalStockGlobal.toFixed(2) }}</h4>
          </div>
          <div class="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center shadow-2xs">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
            </svg>
          </div>
        </div>
      </div>

      <!-- Filtros y Cabecera -->
      <div class="bg-white rounded-2xl p-5 shadow-md border border-slate-100">
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-4 border-b border-slate-100 gap-2">
          <div>
            <h2 class="text-base font-bold text-slate-800">Almacenes y Bodegas de Planta</h2>
            <p class="text-xs text-slate-400 mt-0.5">Control de puntos físicos de resguardo para materias primas, insumos y producto final.</p>
          </div>
          <button 
            v-if="can('ALMACENES', 'insertar')"
            @click="abrirModalCrear"
            class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-2"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
            Nuevo Almacén
          </button>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 pt-4 items-center">
          <div class="sm:col-span-2">
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Buscar por Nombre de Bodega</label>
            <input 
              v-model="filtroBusqueda" 
              type="text" 
              placeholder="Ej: Bodega Central o Almacén de Telas..." 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500"
            />
          </div>

          <div class="flex items-center pt-5 sm:pt-4">
            <label class="flex items-center gap-2 text-xs font-semibold text-slate-600 cursor-pointer select-none">
              <input 
                type="checkbox" 
                v-model="mostrarInactivos" 
                class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500"
              />
              <span>Mostrar almacenes inactivos</span>
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
                <th class="py-3 px-4">Nombre del Almacén</th>
                <th class="py-3 px-4 text-center">Variedad de Artículos</th>
                <th class="py-3 px-4 text-right">Existencia Física Total</th>
                <th class="py-3 px-4 text-center">Estado Operativo</th>
                <th class="py-3 px-4 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="item in almacenesFiltrados" :key="item.idAlmacen" class="hover:bg-slate-50/60 transition">
                <td class="py-3.5 px-4 font-bold text-slate-800 flex items-center gap-2.5">
                  <div class="w-8 h-8 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center font-bold text-xs shadow-2xs">
                    #{{ item.idAlmacen }}
                  </div>
                  {{ item.nombreAlmacen }}
                </td>
                <td class="py-3.5 px-4 text-center">
                  <span class="px-2.5 py-0.5 rounded-md text-[10px] font-bold bg-indigo-50 border border-indigo-200/80 text-indigo-700">
                    {{ item.totalArticulosDistintos }} artículo(s)
                  </span>
                </td>
                <td class="py-3.5 px-4 text-right font-mono font-bold text-slate-700">
                  {{ item.totalUnidadesStock.toFixed(2) }}
                </td>
                <td class="py-3.5 px-4 text-center">
                  <span :class="['px-2.5 py-0.5 rounded-full text-[10px] font-bold', item.estadoAlmacen ? 'bg-emerald-50 text-emerald-600 border border-emerald-200' : 'bg-rose-50 text-rose-600 border border-rose-200']">
                    {{ item.estadoAlmacen ? 'Operativo' : 'Inactivo' }}
                  </span>
                </td>
                <td class="py-3.5 px-4 text-right">
                  <div class="flex items-center justify-end gap-1.5">
                    <button 
                      @click="verInventarioBodega(item)"
                      class="px-2.5 py-1 bg-purple-50 hover:bg-purple-600 text-purple-700 hover:text-white rounded-lg text-xs font-semibold transition shadow-2xs"
                      title="Ver existencias en bodega"
                    >
                      Ver Stock
                    </button>
                    <button 
                      v-if="can('ALMACENES', 'modificar')"
                      @click="abrirModalEditar(item)"
                      class="px-2.5 py-1 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition shadow-2xs"
                    >
                      Editar
                    </button>
                    <button 
                      v-if="can('ALMACENES', 'eliminar')"
                      @click="solicitarToggleEstado(item)"
                      :class="[
                        'px-2.5 py-1 rounded-lg text-xs font-semibold transition border shadow-2xs',
                        item.estadoAlmacen ? 'bg-rose-50 hover:bg-rose-600 text-rose-600 hover:text-white border-rose-200' : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-600 hover:text-white border-emerald-200'
                      ]"
                    >
                      {{ item.estadoAlmacen ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>
              <tr v-if="almacenesFiltrados.length === 0 && !isLoading">
                <td colspan="5" class="py-8 text-center text-slate-400 text-xs italic">
                  No se encontraron almacenes registrados con los filtros aplicados.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Formulario Creación / Edición -->
    <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 animate-scale-in">
        <h3 class="text-base font-bold text-slate-800 mb-1">{{ isEditing ? 'Editar Almacén' : 'Nuevo Almacén / Bodega' }}</h3>
        <p class="text-xs text-slate-400 mb-4">Ingresa el nombre descriptivo del punto de almacenamiento.</p>

        <form @submit.prevent="guardarAlmacen" class="space-y-3.5">
          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Nombre del Almacén</label>
            <input 
              v-model="formAlmacen.nombreAlmacen" 
              @keypress="allowOnly.nombreInput($event)"
              required 
              maxlength="80"
              type="text" 
              placeholder="Ej: Bodega Central de Telas" 
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500" 
            />
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-100">
            <button type="button" @click="showModal = false" class="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl transition">Cancelar</button>
            <button type="submit" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
              {{ isEditing ? 'Guardar Cambios' : 'Registrar Almacén' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Detalle de Stock Físico por Almacén -->
    <div v-if="showDetalleModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-2xl border border-slate-100 max-h-[85vh] flex flex-col animate-scale-in">
        <div class="flex items-center justify-between pb-3 border-b border-slate-100">
          <div>
            <h3 class="text-sm font-bold text-slate-800">Inventario en {{ almacenSeleccionado?.nombreAlmacen }}</h3>
            <p class="text-[11px] text-slate-400">Existencias físicas activas registradas en esta bodega.</p>
          </div>
          <button @click="showDetalleModal = false" class="text-slate-400 hover:text-slate-600 text-base">✕</button>
        </div>

        <div class="my-4 overflow-y-auto pr-1 flex-1">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                <th class="py-2.5 px-3">Código</th>
                <th class="py-2.5 px-3">Artículo</th>
                <th class="py-2.5 px-3">Tipo</th>
                <th class="py-2.5 px-3 text-right">Existencia</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="det in inventarioDetalle" :key="det.idArticulo" class="hover:bg-slate-50/60 transition">
                <td class="py-2.5 px-3 font-mono font-bold text-purple-700 text-[11px]">{{ det.codigoArticulo }}</td>
                <td class="py-2.5 px-3 font-semibold text-slate-800">{{ det.nombreArticulo }}</td>
                <td class="py-2.5 px-3 text-slate-500 text-[11px]">{{ det.tipoArticulo }}</td>
                <td class="py-2.5 px-3 text-right font-mono font-bold text-slate-900">
                  {{ det.stockActual.toFixed(2) }} {{ det.unidadMedida }}
                </td>
              </tr>
              <tr v-if="inventarioDetalle.length === 0 && !isLoadingDetalle">
                <td colspan="4" class="py-8 text-center text-slate-400 text-xs italic">
                  Este almacén no cuenta con existencias físicas en este momento.
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="flex justify-end pt-3 border-t border-slate-100">
          <button @click="showDetalleModal = false" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
            Cerrar
          </button>
        </div>
      </div>
    </div>
  </MainLayout>
</template>