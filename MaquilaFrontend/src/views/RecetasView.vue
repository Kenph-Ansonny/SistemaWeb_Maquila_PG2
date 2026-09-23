<script setup>
import { ref, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import recetaService from '../services/recetaService'
import articuloService from '../services/articuloService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

const isSaving = ref(false)

// Estados Reactivos
const recetas = ref([])
const articulos = ref([])
const unidades = ref([])
const isLoading = ref(true)

const showModal = ref(false)
const showDetalleModal = ref(false)
const isEditing = ref(false)
const currentId = ref(null)

const filtroBusqueda = ref('')
const mostrarInactivos = ref(false)
const recetaSeleccionada = ref(null)

// Modales de Sistema
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

// Formularios
const formReceta = ref({
  nombreReceta: '',
  idArticuloPrenda: '',
  descripcionReceta: '',
  detalles: []
})

const itemInsumoForm = ref({
  idArticuloInsumo: '',
  idUnidadConsumo: '',
  cantidadNeta: 1,
  porcentajeMerma: 0
})

// Funciones Auxiliares
const showAlert = (title, message, type = 'success') => {
  alertModal.value = { show: true, title, message, type }
}

const formatMoneda = (val) => {
  return new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(val || 0)
}

// Filtros y Computados
const prendasTerminadas = computed(() => {
  return articulos.value.filter(a => a.tipoArticulo === 'PrendaTerminada' && a.estadoArticulo)
})

const insumosDisponibles = computed(() => {
  return articulos.value.filter(a => ['MateriaPrima', 'Insumo', 'PiezaCorte'].includes(a.tipoArticulo) && a.estadoArticulo)
})

const recetasFiltradas = computed(() => {
  return recetas.value.filter(r => {
    if (!mostrarInactivos.value && !r.estadoReceta) return false

    if (filtroBusqueda.value.trim()) {
      const term = filtroBusqueda.value.toLowerCase()
      const matchNombre = r.nombreReceta?.toLowerCase().includes(term)
      const matchPrenda = r.nombrePrenda?.toLowerCase().includes(term)
      const matchCodigo = r.codigoPrenda?.toLowerCase().includes(term)
      return matchNombre || matchPrenda || matchCodigo
    }
    return true
  })
})

const cantidadBrutaCalculadaItem = computed(() => {
  const neta = Number(itemInsumoForm.value.cantidadNeta) || 0
  const merma = Number(itemInsumoForm.value.porcentajeMerma) || 0
  const bruta = neta * (1 + merma / 100)
  return bruta.toFixed(4)
})

const costoTotalEstimado = computed(() => {
  return formReceta.value.detalles.reduce((acc, det) => {
    const art = articulos.value.find(a => a.idArticulo === det.idArticuloInsumo)
    const costo = art ? (art.costoPromedio || 0) : 0
    return acc + (costo * det.cantidadBruta)
  }, 0)
})

// Carga Inicial de Datos
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
    recetas.value = resRecetas.data
    articulos.value = resArticulos.data
    unidades.value = resUnidades.data
  } catch {
    showAlert('Error de Sincronización', 'No se pudieron obtener las fichas técnicas desde el servidor.', 'error')
  } finally {
    isLoading.value = false
  }
}

// Gestión de Insumos en la Ficha Técnica
const onInsumoChange = () => {
  const insumo = articulos.value.find(a => a.idArticulo === itemInsumoForm.value.idArticuloInsumo)
  if (insumo) {
    itemInsumoForm.value.idUnidadConsumo = insumo.idUnidadBaseMedida
  }
}

const agregarInsumo = () => {
  const { idArticuloInsumo, idUnidadConsumo, cantidadNeta, porcentajeMerma } = itemInsumoForm.value

  if (!idArticuloInsumo) {
    showAlert('Campo Requerido', 'Selecciona un insumo o tela para agregar.', 'error')
    return
  }

  if (!idUnidadConsumo) {
    showAlert('Campo Requerido', 'Selecciona la unidad de consumo para este insumo.', 'error')
    return
  }

  if (cantidadNeta <= 0) {
    showAlert('Cantidad Inválida', 'La cantidad neta debe ser mayor a cero.', 'error')
    return
  }

  if (porcentajeMerma < 0 || porcentajeMerma > 100) {
    showAlert('Merma Inválida', 'El porcentaje de merma debe estar entre 0% y 100%.', 'error')
    return
  }

  const existe = formReceta.value.detalles.some(d => d.idArticuloInsumo === idArticuloInsumo)
  if (existe) {
    showAlert('Insumo Duplicado', 'Este insumo ya está incluido en la ficha. Elimínalo si deseas cambiar sus cantidades.', 'error')
    return
  }

  const neta = Number(cantidadNeta)
  const merma = Number(porcentajeMerma) || 0
  const bruta = Number((neta * (1 + merma / 100)).toFixed(4))

  formReceta.value.detalles.push({
    idArticuloInsumo,
    idUnidadConsumo,
    cantidadNeta: neta,
    porcentajeMerma: merma,
    cantidadBruta: bruta
  })

  // Reset del formulario de inserción
  itemInsumoForm.value.cantidadNeta = 1
  itemInsumoForm.value.porcentajeMerma = 0
}

const quitarInsumo = (index) => {
  formReceta.value.detalles.splice(index, 1)
}

// Control de Modales
const abrirModalCrear = () => {
  if (!can('RECETAS', 'insertar')) return

  if (prendasTerminadas.value.length === 0) {
    showAlert('Sin Prendas Disponibles', 'Debes registrar al menos una prenda terminada activa antes de crear una ficha técnica.', 'error')
    return
  }

  isEditing.value = false
  currentId.value = null

  formReceta.value = {
    nombreReceta: '',
    idArticuloPrenda: prendasTerminadas.value[0]?.idArticulo || '',
    descripcionReceta: '',
    detalles: []
  }

  if (insumosDisponibles.value.length > 0) {
    itemInsumoForm.value.idArticuloInsumo = insumosDisponibles.value[0].idArticulo
    onInsumoChange()
  }

  showModal.value = true
}

const abrirModalEditar = async (item) => {
  if (!can('RECETAS', 'modificar')) return
  isEditing.value = true
  currentId.value = item.idReceta

  try {
    const res = await recetaService.obtenerDetalle(item.idReceta)
    const detalleData = res.data

    formReceta.value = {
      nombreReceta: detalleData.nombreReceta,
      idArticuloPrenda: detalleData.idArticuloPrenda,
      descripcionReceta: detalleData.descripcionReceta || '',
      detalles: detalleData.detalles.map(d => ({
        idArticuloInsumo: d.idArticuloInsumo,
        idUnidadConsumo: d.idUnidadConsumo,
        cantidadNeta: d.cantidadNeta,
        porcentajeMerma: d.porcentajeMerma,
        cantidadBruta: d.cantidadBruta
      }))
    }

    if (insumosDisponibles.value.length > 0) {
      itemInsumoForm.value.idArticuloInsumo = insumosDisponibles.value[0].idArticulo
      onInsumoChange()
    }

    showModal.value = true
  } catch (err) {
    showAlert('Error', err.response?.data?.message || 'No se pudo cargar la receta.', 'error')
  }
}

const verDetalle = async (item) => {
  recetaSeleccionada.value = null
  try {
    const res = await recetaService.obtenerDetalle(item.idReceta)
    recetaSeleccionada.value = res.data
    showDetalleModal.value = true
  } catch (err) {
    showAlert('Error', err.response?.data?.message || 'No se pudo obtener el desglose.', 'error')
  }
}

// Guardar / Actualizar
const guardarReceta = async () => {
  const nombre = formReceta.value.nombreReceta?.trim()
  const descripcion = formReceta.value.descripcionReceta?.trim() || ''

  if (!TextRules.esNombreValido(nombre, 3, 100)) {
    showAlert('Nombre Inválido', 'El nombre de la receta debe tener entre 3 y 100 caracteres válidos.', 'error')
    return
  }

  if (descripcion && !TextRules.esDescripcionValida(descripcion, 500)) {
    showAlert('Descripción Inválida', 'La descripción contiene símbolos no admitidos o excede los 500 caracteres.', 'error')
    return
  }

  if (!formReceta.value.idArticuloPrenda) {
    showAlert('Prenda Requerida', 'Debes seleccionar una prenda terminada destino.', 'error')
    return
  }

  if (formReceta.value.detalles.length === 0) {
    showAlert('Insumos Requeridos', 'Debes agregar al menos un insumo o tela a la ficha técnica.', 'error')
    return
  }

  if (isSaving.value) return
  isSaving.value = true

  try {
    const payload = {
      ...formReceta.value,
      nombreReceta: nombre,
      descripcionReceta: descripcion || null
    }

    if (isEditing.value) {
      await recetaService.actualizar(currentId.value, payload)
      showAlert('Modificación Exitosa', 'La ficha técnica ha sido actualizada correctamente.')
    } else {
      await recetaService.crear(payload)
      showAlert('Registro Exitoso', 'La nueva ficha técnica (BOM) ha sido guardada.')
    }
    showModal.value = false
    cargarDatos()
  } catch (err) {
    showAlert('Error al Procesar', err.response?.data?.message || 'Ocurrió un fallo en el servidor.', 'error')
  } finally {
    isSaving.value = false
  }
}

// Toggle Estado
const solicitarToggleEstado = (item) => {
  if (!can('RECETAS', 'eliminar')) return
  const accion = item.estadoReceta ? 'desactivar' : 'activar'

  confirmModal.value = {
    show: true,
    title: '¿Confirmar cambio de estado?',
    message: `¿Deseas ${accion} la ficha técnica "${item.nombreReceta}"?`,
    action: async () => {
      confirmModal.value.show = false
      try {
        const res = await recetaService.cambiarEstado(item.idReceta)
        showAlert('Estado Actualizado', res.data.message)
        cargarDatos()
      } catch (err) {
        showAlert('Error', err.response?.data?.message || 'No se pudo cambiar el estado.', 'error')
      }
    },
    type: item.estadoReceta ? 'danger' : 'success'
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

    <!-- Restricción Permiso -->
    <div v-if="!can('RECETAS', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
      </div>
      <h3 class="text-sm font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-400 mt-1">No cuentas con autorización para consultar las fichas técnicas.</p>
    </div>

    <!-- Vista Principal -->
    <div v-else class="space-y-4">
      <div class="bg-white rounded-2xl p-5 shadow-md border border-slate-100">
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-4 border-b border-slate-100 gap-2">
          <div>
            <h2 class="text-base font-bold text-slate-800">Fichas Técnicas de Confección (BOM)</h2>
            <p class="text-xs text-slate-400 mt-0.5">Control de consumo neto, porcentaje de merma en corte y cantidad bruta requerida.</p>
          </div>
          <button
            v-if="can('RECETAS', 'insertar')"
            @click="abrirModalCrear"
            class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-semibold text-xs rounded-xl shadow-md hover:opacity-95 transition flex items-center gap-2"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
            Nueva Ficha Técnica
          </button>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 pt-4 items-center">
          <div class="sm:col-span-2">
            <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase tracking-wider">Buscar Ficha o Prenda</label>
            <input
              v-model="filtroBusqueda"
              type="text"
              placeholder="Ej: Ficha Playera Polo o TEL-001..."
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
              <span>Mostrar recetas inactivas</span>
            </label>
          </div>
        </div>
      </div>

      <!-- Tabla de Recetas -->
      <div class="bg-white rounded-2xl p-6 shadow-md border border-slate-100">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 text-[11px] font-bold text-slate-400 uppercase tracking-wider">
                <th class="py-3 px-4">Ficha / Receta</th>
                <th class="py-3 px-4">Prenda Terminada</th>
                <th class="py-3 px-4">Variantes</th>
                <th class="py-3 px-4 text-center">Insumos</th>
                <th class="py-3 px-4 text-right">Costo Estimado</th>
                <th class="py-3 px-4 text-center">Estado</th>
                <th class="py-3 px-4 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="r in recetasFiltradas" :key="r.idReceta" class="hover:bg-slate-50/60 transition">
                <td class="py-3.5 px-4 font-bold text-slate-800">
                  <span class="block text-slate-900">{{ r.nombreReceta }}</span>
                  <span class="text-[10px] text-purple-700 font-mono">#REC-{{ r.idReceta }}</span>
                </td>
                <td class="py-3.5 px-4 text-slate-700 font-medium">
                  <span class="block font-bold text-slate-800">{{ r.nombrePrenda }}</span>
                  <span class="text-[10px] text-slate-400 font-mono">{{ r.codigoPrenda }}</span>
                </td>
                <td class="py-3.5 px-4">
                  <div class="flex items-center gap-1.5">
                    <span v-if="r.talla" class="px-1.5 py-0.5 rounded bg-slate-100 text-slate-700 text-[10px] font-bold">T: {{ r.talla }}</span>
                    <span v-if="r.color" class="px-1.5 py-0.5 rounded bg-slate-100 text-slate-700 text-[10px] font-bold">C: {{ r.color }}</span>
                    <span v-if="!r.talla && !r.color" class="text-slate-400 font-mono text-[11px]">—</span>
                  </div>
                </td>
                <td class="py-3.5 px-4 text-center">
                  <span class="px-2.5 py-0.5 rounded-md text-[10px] font-bold bg-indigo-50 border border-indigo-200/80 text-indigo-700">
                    {{ r.totalInsumos }} componentes
                  </span>
                </td>
                <td class="py-3.5 px-4 text-right font-mono font-bold text-slate-800">
                  {{ formatMoneda(r.costoEstimadoTotal) }}
                </td>
                <td class="py-3.5 px-4 text-center">
                  <span :class="['px-2.5 py-0.5 rounded-full text-[10px] font-bold', r.estadoReceta ? 'bg-emerald-50 text-emerald-600 border border-emerald-200' : 'bg-rose-50 text-rose-600 border border-rose-200']">
                    {{ r.estadoReceta ? 'Activa' : 'Inactiva' }}
                  </span>
                </td>
                <td class="py-3.5 px-4 text-right">
                  <div class="flex items-center justify-end gap-1.5">
                    <button
                      @click="verDetalle(r)"
                      class="px-2.5 py-1 bg-purple-50 hover:bg-purple-600 text-purple-700 hover:text-white rounded-lg text-xs font-semibold transition shadow-2xs"
                    >
                      Fórmula
                    </button>
                    <button
                      v-if="can('RECETAS', 'modificar')"
                      @click="abrirModalEditar(r)"
                      class="px-2.5 py-1 bg-slate-100 hover:bg-purple-600 text-slate-600 hover:text-white rounded-lg text-xs font-semibold transition shadow-2xs"
                    >
                      Editar
                    </button>
                    <button
                      v-if="can('RECETAS', 'eliminar')"
                      @click="solicitarToggleEstado(r)"
                      :class="[
                        'px-2.5 py-1 rounded-lg text-xs font-semibold transition border shadow-2xs',
                        r.estadoReceta ? 'bg-rose-50 hover:bg-rose-600 text-rose-600 hover:text-white border-rose-200' : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-600 hover:text-white border-emerald-200'
                      ]"
                    >
                      {{ r.estadoReceta ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>
              <tr v-if="recetasFiltradas.length === 0 && !isLoading">
                <td colspan="7" class="py-8 text-center text-slate-400 text-xs italic">
                  No se encontraron fichas técnicas registradas.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Formulario Creación / Edición -->
    <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-2xl border border-slate-100 max-h-[90vh] overflow-y-auto animate-scale-in">
        <h3 class="text-base font-bold text-slate-800 mb-1">{{ isEditing ? 'Editar Ficha Técnica' : 'Nueva Ficha Técnica (BOM)' }}</h3>
        <p class="text-xs text-slate-400 mb-4">Configura la fórmula de materiales y tolerancias de desperdicio.</p>

        <form @submit.prevent="guardarReceta" class="space-y-4">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Nombre de la Receta</label>
              <input
                v-model="formReceta.nombreReceta"
                @keypress="allowOnly.nombreInput($event)"
                required
                maxlength="100"
                type="text"
                placeholder="Ej: Playera Polo Clásica Piqué"
                class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500"
              />
            </div>
            <div>
              <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Prenda Terminada Destino</label>
              <select
                v-model="formReceta.idArticuloPrenda"
                required
                class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500 text-slate-700 font-semibold"
              >
                <option v-for="pt in prendasTerminadas" :key="pt.idArticulo" :value="pt.idArticulo">
                  {{ pt.nombreArticulo }} ({{ pt.codigoArticulo }})
                </option>
              </select>
            </div>
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-600 mb-1 uppercase tracking-wider">Descripción Técnica</label>
            <textarea
              v-model="formReceta.descripcionReceta"
              @keypress="allowOnly.descripcionInput($event)"
              rows="2"
              maxlength="500"
              placeholder="Instrucciones de corte, armado o consideraciones del patronaje..."
              class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs outline-none focus:border-purple-500"
            ></textarea>
          </div>

          <!-- Selector de Insumos con cálculo reactivo de merma -->
          <div class="p-3 bg-slate-50 border border-slate-200 rounded-xl space-y-3">
            <div class="flex items-center justify-between">
              <h4 class="text-xs font-bold text-slate-700 uppercase tracking-wider">Agregar Insumo o Tela</h4>
              <span class="text-[11px] font-mono font-bold text-purple-700">
                Bruta Calculada: {{ cantidadBrutaCalculadaItem }}
              </span>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-12 gap-2 items-end">
              <div class="sm:col-span-4">
                <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Insumo</label>
                <select
                  v-model="itemInsumoForm.idArticuloInsumo"
                  @change="onInsumoChange"
                  class="w-full px-2 py-1.5 bg-white border border-slate-200 rounded-lg text-xs outline-none focus:border-purple-500 text-slate-700 font-semibold"
                >
                  <option v-for="ins in insumosDisponibles" :key="ins.idArticulo" :value="ins.idArticulo">
                    {{ ins.nombreArticulo }} ({{ ins.codigoArticulo }})
                  </option>
                </select>
              </div>

              <div class="sm:col-span-3">
                <label class="block text-[11px] font-bold text-slate-600 mb-1 uppercase">Unidad Consumo</label>
                <select
                  v-model="itemInsumoForm.idUnidadConsumo"
                  class="w-full px-2 py-1.5 bg-white border border-slate-200 rounded-lg text-xs outline-none focus:border-purple-500 text-slate-700 font-semibold"
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
                  class="w-full px-2 py-1.5 bg-white border border-slate-200 rounded-lg text-xs outline-none focus:border-purple-500 font-mono font-bold"
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
                  class="w-full px-2 py-1.5 bg-white border border-slate-200 rounded-lg text-xs outline-none focus:border-purple-500 font-mono font-bold"
                />
              </div>

              <div class="sm:col-span-1">
                <button
                  type="button"
                  @click="agregarInsumo"
                  class="w-full py-1.5 bg-purple-600 hover:bg-purple-700 text-white font-bold text-xs rounded-lg shadow-sm transition"
                >
                  +
                </button>
              </div>
            </div>
          </div>

          <!-- Componentes agregados -->
          <div class="overflow-x-auto border border-slate-200 rounded-xl">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="border-b border-slate-200 bg-slate-50 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                  <th class="py-2 px-3">Insumo</th>
                  <th class="py-2 px-3 text-right">Cant. Neta</th>
                  <th class="py-2 px-3 text-right">% Merma</th>
                  <th class="py-2 px-3 text-right">Cant. Bruta</th>
                  <th class="py-2 px-3 text-right">Subtotal Est.</th>
                  <th class="py-2 px-3 text-center">Quitar</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-for="(det, index) in formReceta.detalles" :key="det.idArticuloInsumo" class="hover:bg-slate-50/50">
                  <td class="py-2 px-3 font-semibold text-slate-800">
                    {{ articulos.find(a => a.idArticulo === det.idArticuloInsumo)?.nombreArticulo }}
                    <span class="text-[10px] text-purple-700 font-mono">({{ articulos.find(a => a.idArticulo === det.idArticuloInsumo)?.codigoArticulo }})</span>
                  </td>
                  <td class="py-2 px-3 text-right font-mono">
                    {{ det.cantidadNeta }} {{ unidades.find(u => u.idUnidadMedida === det.idUnidadConsumo)?.codigoMedida }}
                  </td>
                  <td class="py-2 px-3 text-right font-mono text-amber-600 font-bold">
                    {{ det.porcentajeMerma }}%
                  </td>
                  <td class="py-2 px-3 text-right font-mono font-bold text-slate-900">
                    {{ det.cantidadBruta }} {{ unidades.find(u => u.idUnidadMedida === det.idUnidadConsumo)?.codigoMedida }}
                  </td>
                  <td class="py-2 px-3 text-right font-mono text-slate-700">
                    {{ formatMoneda((articulos.find(a => a.idArticulo === det.idArticuloInsumo)?.costoPromedio || 0) * det.cantidadBruta) }}
                  </td>
                  <td class="py-2 px-3 text-center">
                    <button
                      type="button"
                      @click="quitarInsumo(index)"
                      class="text-rose-500 hover:text-rose-700 font-bold text-xs"
                    >
                      ✕
                    </button>
                  </td>
                </tr>
                <tr v-if="formReceta.detalles.length === 0">
                  <td colspan="6" class="py-6 text-center text-slate-400 text-xs italic">
                    No se han asignado insumos a esta ficha.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="flex items-center justify-between p-3 bg-purple-50/50 border border-purple-100 rounded-xl">
            <span class="text-xs font-bold text-slate-700 uppercase">Costo Estimado Real por Prenda (con Merma):</span>
            <span class="text-sm font-mono font-black text-purple-700">{{ formatMoneda(costoTotalEstimado) }}</span>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-100">
            <button type="button" @click="showModal = false" class="px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-100 rounded-xl transition">Cancelar</button>
            <button
              type="submit"
              :disabled="isSaving"
              class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {{ isSaving ? 'Guardando...' : (isEditing ? 'Guardar Cambios' : 'Registrar Ficha') }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Detalle Fórmula -->
    <div v-if="showDetalleModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
      <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-2xl border border-slate-100 max-h-[85vh] flex flex-col animate-scale-in">
        <div class="flex items-center justify-between pb-3 border-b border-slate-100">
          <div>
            <h3 class="text-sm font-bold text-slate-800">Fórmula: {{ recetaSeleccionada?.nombreReceta }}</h3>
            <p class="text-[11px] text-slate-400">Prenda: {{ recetaSeleccionada?.nombreArticulo }} ({{ recetaSeleccionada?.codigoArticulo }})</p>
          </div>
          <button @click="showDetalleModal = false" class="text-slate-400 hover:text-slate-600 text-base">✕</button>
        </div>

        <div class="my-4 overflow-y-auto pr-1 flex-1">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                <th class="py-2.5 px-3">Componente</th>
                <th class="py-2.5 px-3 text-right">Neta</th>
                <th class="py-2.5 px-3 text-right">% Merma</th>
                <th class="py-2.5 px-3 text-right">Bruta Real</th>
                <th class="py-2.5 px-3 text-right">Subtotal</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-for="d in recetaSeleccionada?.detalles" :key="d.idArticuloInsumo" class="hover:bg-slate-50/60">
                <td class="py-2.5 px-3 font-semibold text-slate-800">
                  <span class="block">{{ d.nombreInsumo }}</span>
                  <span class="text-[10px] text-purple-700 font-mono">{{ d.codigoInsumo }}</span>
                </td>
                <td class="py-2.5 px-3 text-right font-mono text-slate-600">
                  {{ d.cantidadNeta?.toFixed(4) }} {{ d.codigoMedida }}
                </td>
                <td class="py-2.5 px-3 text-right font-mono font-bold text-amber-600">
                  {{ d.porcentajeMerma?.toFixed(2) }}%
                </td>
                <td class="py-2.5 px-3 text-right font-mono font-bold text-slate-900">
                  {{ d.cantidadBruta?.toFixed(4) }} {{ d.codigoMedida }}
                </td>
                <td class="py-2.5 px-3 text-right font-mono font-bold text-purple-700">
                  {{ formatMoneda(d.subtotal) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="flex items-center justify-between pt-3 border-t border-slate-100">
          <span class="text-xs font-bold text-slate-700 uppercase">Costo Total Estimado:</span>
          <span class="text-sm font-mono font-black text-purple-700">{{ formatMoneda(recetaSeleccionada?.costoEstimadoTotal) }}</span>
        </div>

        <div class="flex justify-end pt-3">
          <button @click="showDetalleModal = false" class="px-4 py-2 text-xs font-semibold text-white bg-purple-600 hover:bg-purple-700 rounded-xl shadow-md transition">
            Cerrar
          </button>
        </div>
      </div>
    </div>
  </MainLayout>
</template>