<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import clienteService from '../services/clienteService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

// ---------- Estado Principal ----------
const clientes = ref([])
const isLoading = ref(true)
const isSaving = ref(false)

const filtroBusqueda = ref('')
const filtroEstado = ref('') // '' = Todos, 'activos', 'inactivos'

// ---------- Modales ----------
const showModal = ref(false)
const isEditing = ref(false)
const currentId = ref(null)

// Respaldo de los datos originales del cliente antes de editar
const clienteOriginal = ref(null)

// Modal de Confirmación de Cambios (Estilo idéntico a la imagen)
const showConfirmGuardar = ref(false)

const formCliente = reactive({
  nombreCliente: '',
  telefonoCliente: '',
  direccionCliente: ''
})
const erroresForm = reactive({})

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

const showAlert = (title, message, type = 'success') => {
  alertModal.value = { show: true, title, message, type }
}

// ---------- Detección y Comparación de Cambios ----------
const resumenCambios = computed(() => {
  if (!isEditing.value || !clienteOriginal.value) return []

  const nomActual = (formCliente.nombreCliente || '').trim()
  const nomAnt = (clienteOriginal.value.nombreCliente || '').trim()

  const telActual = (formCliente.telefonoCliente || '').trim()
  const telAnt = (clienteOriginal.value.telefonoCliente || '').trim()

  const dirActual = (formCliente.direccionCliente || '').trim()
  const dirAnt = (clienteOriginal.value.direccionCliente || '').trim()

  return [
    {
      campo: 'nombreCliente',
      etiqueta: 'Nombre / Razón Social',
      anterior: nomAnt,
      nuevo: nomActual,
      haCambiado: nomActual !== nomAnt
    },
    {
      campo: 'telefonoCliente',
      etiqueta: 'Teléfono de Contacto',
      anterior: telAnt || 'Sin teléfono',
      nuevo: telActual || 'Sin teléfono',
      haCambiado: telActual !== telAnt
    },
    {
      campo: 'direccionCliente',
      etiqueta: 'Dirección Fiscal / Entrega',
      anterior: dirAnt || 'Sin dirección',
      nuevo: dirActual || 'Sin dirección',
      haCambiado: dirActual !== dirAnt
    }
  ]
})

const cambiosDetectados = computed(() => resumenCambios.value.filter(c => c.haCambiado))

// ---------- KPIs ----------
const kpis = computed(() => {
  const lista = clientes.value
  return {
    total: lista.length,
    activos: lista.filter(c => c.estadoCliente).length,
    conPedidos: lista.filter(c => c.totalPedidos > 0).length,
    inactivos: lista.filter(c => !c.estadoCliente).length
  }
})

// ---------- Filtrado Reactivo ----------
const clientesFiltrados = computed(() => {
  return clientes.value.filter(c => {
    if (filtroEstado.value === 'activos' && !c.estadoCliente) return false
    if (filtroEstado.value === 'inactivos' && c.estadoCliente) return false

    if (filtroBusqueda.value.trim()) {
      const term = filtroBusqueda.value.toLowerCase().trim()
      const matchNombre = c.nombreCliente.toLowerCase().includes(term)
      const matchTelefono = c.telefonoCliente ? c.telefonoCliente.toLowerCase().includes(term) : false
      const matchDireccion = c.direccionCliente ? c.direccionCliente.toLowerCase().includes(term) : false
      return matchNombre || matchTelefono || matchDireccion
    }
    return true
  })
})

// ---------- Carga de Datos ----------
const cargarClientes = async () => {
  if (!can('CLIENTES', 'consultar')) {
    isLoading.value = false
    return
  }

  isLoading.value = true
  try {
    const data = await clienteService.obtenerTodos()
    clientes.value = Array.isArray(data) ? data : (data?.data || [])
  } catch (err) {
    showAlert('Error de Comunicación', err?.response?.data?.message || 'No se pudo sincronizar el catálogo de clientes.', 'error')
  } finally {
    isLoading.value = false
  }
}

// ---------- Apertura de Formularios ----------
const abrirModalCrear = () => {
  if (!can('CLIENTES', 'insertar')) return
  isEditing.value = false
  currentId.value = null
  clienteOriginal.value = null
  formCliente.nombreCliente = ''
  formCliente.telefonoCliente = ''
  formCliente.direccionCliente = ''
  limpiarErrores()
  showModal.value = true
}

const abrirModalEditar = (cliente) => {
  if (!can('CLIENTES', 'modificar')) return
  isEditing.value = true
  currentId.value = cliente.idCliente

  // Copia exacta para comparar diferencias
  clienteOriginal.value = {
    nombreCliente: cliente.nombreCliente || '',
    telefonoCliente: cliente.telefonoCliente || '',
    direccionCliente: cliente.direccionCliente || ''
  }

  formCliente.nombreCliente = cliente.nombreCliente || ''
  formCliente.telefonoCliente = cliente.telefonoCliente || ''
  formCliente.direccionCliente = cliente.direccionCliente || ''

  limpiarErrores()
  showModal.value = true
}

const limpiarErrores = () => {
  Object.keys(erroresForm).forEach(k => delete erroresForm[k])
}

// ---------- Validaciones del Formulario ----------
const validarFormulario = () => {
  limpiarErrores()
  const nom = formCliente.nombreCliente?.trim() || ''
  const tel = formCliente.telefonoCliente?.trim() || ''
  const dir = formCliente.direccionCliente?.trim() || ''

  if (!nom) {
    erroresForm.nombreCliente = 'El nombre o razón social es obligatorio.'
  } else if (!TextRules.esNombreValido(nom, 3, 150)) {
    erroresForm.nombreCliente = 'El nombre debe contener entre 3 y 150 caracteres válidos.'
  }

  if (tel && !TextRules.esTelefonoValido(tel)) {
    erroresForm.telefonoCliente = 'Formato no válido (números, +, guiones, espacios y paréntesis, 7-20 caracteres).'
  }

  if (dir && !TextRules.esDescripcionValida(dir, 250)) {
    erroresForm.direccionCliente = 'La dirección excede la longitud máxima (250 caracteres).'
  }

  return Object.keys(erroresForm).length === 0
}

// ---------- Disparador de Guardado ----------
const solicitarGuardar = () => {
  if (!validarFormulario()) return

  if (isEditing.value) {
    // Si no modificó nada, se le avisa sin llamar al backend
    if (cambiosDetectados.value.length === 0) {
      showAlert('Sin Cambios', 'No realizaste ninguna modificación en los datos del cliente.', 'warning')
      return
    }
    // Abre el modal visual de confirmación con el resumen de cambios
    showConfirmGuardar.value = true
  } else {
    ejecutarGuardado()
  }
}

// ---------- Guardar / Actualizar en Base de Datos ----------
const ejecutarGuardado = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const payload = {
    nombreCliente: formCliente.nombreCliente.trim(),
    telefonoCliente: formCliente.telefonoCliente.trim() || null,
    direccionCliente: formCliente.direccionCliente.trim() || null
  }

  try {
    if (isEditing.value) {
      await clienteService.actualizar(currentId.value, payload)
      showAlert('Modificación Exitosa', 'El cliente fue actualizado correctamente.')
    } else {
      await clienteService.crear(payload)
      showAlert('Registro Exitoso', 'El cliente fue registrado en el catálogo.')
    }
    showConfirmGuardar.value = false
    showModal.value = false
    await cargarClientes()
  } catch (err) {
    showConfirmGuardar.value = false
    erroresForm.general = err?.response?.data?.message || 'Ocurrió un error al procesar la solicitud.'
  } finally {
    isSaving.value = false
  }
}

// ---------- Cambio de Estado (Activar/Desactivar) ----------
const solicitarToggleEstado = (cliente) => {
  if (!can('CLIENTES', 'eliminar')) return
  const accion = cliente.estadoCliente ? 'desactivar' : 'activar'
  confirmModal.value = {
    show: true,
    title: `¿${accion.charAt(0).toUpperCase() + accion.slice(1)} Cliente?`,
    message: `¿Estás seguro de que deseas ${accion} al cliente "${cliente.nombreCliente}"?`,
    type: cliente.estadoCliente ? 'danger' : 'success',
    action: async () => {
      confirmModal.value.show = false
      try {
        const res = await clienteService.cambiarEstado(cliente.idCliente)
        showAlert('Estado Actualizado', res?.message || res?.data?.message || `Cliente ${accion}do exitosamente.`)
        await cargarClientes()
      } catch (err) {
        showAlert('Error', err?.response?.data?.message || 'No se pudo cambiar el estado del cliente.', 'error')
      }
    }
  }
}

onMounted(cargarClientes)
</script>

<template>
  <MainLayout>
    <!-- Modal Confirmación de Guardar Cambios (Estilo Idéntico a la Imagen) -->
    <Transition name="fade">
      <div v-if="showConfirmGuardar" class="fixed inset-0 z-[65] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
        <div class="bg-white rounded-3xl p-6 sm:p-7 shadow-2xl w-full max-w-md border border-slate-100 text-center animate-scale-in">
          <!-- Ícono Redondo con ? -->
          <div class="w-16 h-16 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center mx-auto mb-4 border border-purple-100 shadow-sm">
            <svg class="w-8 h-8 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>

          <h3 class="text-lg font-black text-slate-800 tracking-tight">¿Guardar los cambios?</h3>
          <p class="text-xs text-slate-500 font-medium mt-1 mb-5">
            Revisa los datos antes de actualizar el cliente en el catálogo.
          </p>

          <!-- Tarjeta de Resumen Comparativo de Datos -->
          <div class="bg-slate-50/80 border border-slate-200/70 rounded-2xl p-4 text-left space-y-3 mb-6">
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

              <!-- Si cambió, muestra el valor anterior y el nuevo -->
              <div v-if="item.haCambiado" class="mt-1 space-y-0.5">
                <div class="text-[11px] text-slate-400 line-through">
                  <span class="font-semibold text-slate-400">Anterior:</span> {{ item.anterior }}
                </div>
                <div class="text-xs font-bold text-slate-800">
                  <span class="text-purple-600 font-bold">Nuevo:</span> {{ item.nuevo }}
                </div>
              </div>

              <!-- Si no cambió, solo muestra el valor actual -->
              <div v-else class="mt-0.5 text-xs font-semibold text-slate-700">
                {{ item.nuevo }}
              </div>
            </div>
          </div>

          <!-- Botones de Acción -->
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
              class="flex-1 py-2.5 px-4 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 transition-all disabled:opacity-60"
            >
              {{ isSaving ? 'Guardando...' : 'Sí, guardar cambios' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Modal Notificación Global (Alertas de éxito/error) -->
    <Transition name="fade">
      <div v-if="alertModal.show" class="fixed inset-0 z-[70] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
        <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
          <div :class="[
            'w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3 shadow-md',
            alertModal.type === 'success' ? 'bg-emerald-50 text-emerald-600 border border-emerald-200' :
            alertModal.type === 'warning' ? 'bg-amber-50 text-amber-600 border border-amber-200' :
            'bg-rose-50 text-rose-600 border border-rose-200'
          ]">
            <svg v-if="alertModal.type === 'success'" class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7" />
            </svg>
            <svg v-else-if="alertModal.type === 'warning'" class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <svg v-else class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>
          <h3 class="text-base font-black text-slate-800">{{ alertModal.title }}</h3>
          <p class="text-xs text-slate-600 font-medium mt-1 mb-6 leading-relaxed">{{ alertModal.message }}</p>
          <button @click="alertModal.show = false" class="w-full py-2.5 text-xs font-bold text-white bg-gradient-to-tl from-purple-700 to-pink-500 rounded-xl shadow-md hover:shadow-lg transition-all">
            Aceptar
          </button>
        </div>
      </div>
    </Transition>

    <!-- Modal Confirmación para Activar/Desactivar -->
    <Transition name="fade">
      <div v-if="confirmModal.show" class="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
        <div class="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-sm border border-slate-100 text-center animate-scale-in">
          <div :class="['w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3 shadow-md', confirmModal.type === 'danger' ? 'bg-rose-50 text-rose-600 border border-rose-200' : 'bg-emerald-50 text-emerald-600 border border-emerald-200']">
            <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>
          <h3 class="text-base font-black text-slate-800">{{ confirmModal.title }}</h3>
          <p class="text-xs text-slate-600 font-medium mt-1 mb-6 leading-relaxed">{{ confirmModal.message }}</p>
          <div class="flex justify-center gap-2.5">
            <button @click="confirmModal.show = false" class="flex-1 py-2.5 text-xs font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition">
              Cancelar
            </button>
            <button @click="confirmModal.action" :class="['flex-1 py-2.5 text-xs font-bold text-white rounded-xl shadow-md transition', confirmModal.type === 'danger' ? 'bg-rose-600 hover:bg-rose-700' : 'bg-emerald-600 hover:bg-emerald-700']">
              Confirmar
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Restricción de Permiso -->
    <div v-if="!can('CLIENTES', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-14 h-14 rounded-2xl bg-rose-50 text-rose-600 flex items-center justify-center mx-auto mb-3 border border-rose-200 shadow-sm">
        <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
      </div>
      <h3 class="text-base font-black text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-500 font-medium mt-1">No cuentas con autorización para consultar el módulo de clientes.</p>
    </div>

    <!-- Contenedor Principal -->
    <div v-else class="space-y-5">
      <!-- Encabezado con Botón -->
      <div class="bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Catálogo de Clientes</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de cuentas comerciales, contactos y trazabilidad de pedidos de confección</p>
        </div>
        <button
          v-if="can('CLIENTES', 'insertar')"
          @click="abrirModalCrear"
          class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:scale-[1.01] active:scale-[0.98] transition-all"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
          </svg>
          Nuevo Cliente
        </button>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Total Clientes</p>
            <p class="text-2xl font-black text-slate-900 mt-1 font-mono">{{ kpis.total }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center border border-purple-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
            </svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Activos</p>
            <p class="text-2xl font-black text-emerald-600 mt-1 font-mono">{{ kpis.activos }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center border border-emerald-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Con Pedidos</p>
            <p class="text-2xl font-black text-indigo-600 mt-1 font-mono">{{ kpis.conPedidos }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center border border-indigo-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z" />
            </svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Inactivos</p>
            <p class="text-2xl font-black text-rose-600 mt-1 font-mono">{{ kpis.inactivos }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-rose-50 text-rose-600 flex items-center justify-center border border-rose-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
            </svg>
          </div>
        </div>
      </div>

      <!-- Barra de Filtros y Búsqueda -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex flex-col md:flex-row md:items-center gap-3">
        <div class="relative flex-1">
          <input
            v-model="filtroBusqueda"
            type="text"
            placeholder="Buscar cliente por nombre, teléfono o dirección..."
            class="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
          />
          <svg class="w-4 h-4 text-slate-400 absolute left-3.5 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>

        <div class="flex items-center gap-1.5 bg-slate-50 p-1 rounded-xl border border-slate-200">
          <button
            v-for="op in [{ val: '', label: 'Todos' }, { val: 'activos', label: 'Activos' }, { val: 'inactivos', label: 'Inactivos' }]"
            :key="op.val"
            @click="filtroEstado = op.val"
            :class="[
              'px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all',
              filtroEstado === op.val
                ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/60'
            ]"
          >
            {{ op.label }}
          </button>
        </div>
      </div>

      <!-- Tabla de Datos -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <div v-if="isLoading" class="flex flex-col items-center justify-center py-20">
          <svg class="animate-spin w-8 h-8 text-purple-600" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"></path>
          </svg>
          <p class="text-xs font-bold text-slate-600 mt-3">Cargando catálogo de clientes...</p>
        </div>

        <div v-else-if="clientesFiltrados.length === 0" class="text-center py-20 px-4">
          <div class="w-14 h-14 bg-purple-50 text-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-3 border border-purple-100">
            <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <p class="text-sm font-black text-slate-800">No se encontraron clientes registrados</p>
          <p class="text-xs text-slate-500 font-medium mt-1">Ajusta los términos de búsqueda o registra un nuevo cliente.</p>
        </div>

        <div v-else class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 bg-slate-50/70 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                <th class="py-3.5 px-5">Cliente / Razón Social</th>
                <th class="py-3.5 px-4">Teléfono de Contacto</th>
                <th class="py-3.5 px-4">Dirección Fiscal / Entrega</th>
                <th class="py-3.5 px-4 text-center">Pedidos Registrados</th>
                <th class="py-3.5 px-4 text-center">Estado</th>
                <th class="py-3.5 px-5 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr
                v-for="cliente in clientesFiltrados"
                :key="cliente.idCliente"
                class="hover:bg-purple-50/20 transition-colors"
              >
                <td class="py-3.5 px-5">
                  <div class="flex items-center gap-3">
                    <div class="w-9 h-9 rounded-xl bg-purple-100/70 text-purple-700 flex items-center justify-center font-bold text-xs shrink-0 border border-purple-200">
                      {{ cliente.nombreCliente.charAt(0).toUpperCase() }}
                    </div>
                    <div>
                      <span class="block text-slate-900 font-bold text-[13px] leading-tight">
                        {{ cliente.nombreCliente }}
                      </span>
                      <span class="text-[10px] font-mono font-bold text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-200 inline-block mt-0.5">
                        ID: #{{ cliente.idCliente }}
                      </span>
                    </div>
                  </div>
                </td>

                <td class="py-3.5 px-4">
                  <div v-if="cliente.telefonoCliente" class="flex items-center gap-1.5 text-slate-800 font-bold font-mono">
                    <svg class="w-3.5 h-3.5 text-emerald-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                    </svg>
                    <span>{{ cliente.telefonoCliente }}</span>
                  </div>
                  <span v-else class="text-slate-400 font-mono text-xs">—</span>
                </td>

                <td class="py-3.5 px-4 max-w-xs">
                  <span v-if="cliente.direccionCliente" class="text-slate-700 font-medium line-clamp-2" :title="cliente.direccionCliente">
                    {{ cliente.direccionCliente }}
                  </span>
                  <span v-else class="text-slate-400 font-mono text-xs">—</span>
                </td>

                <td class="py-3.5 px-4 text-center">
                  <span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-[11px] font-bold bg-indigo-50 border border-indigo-200 text-indigo-700 font-mono shadow-xs">
                    <svg class="w-3 h-3 text-indigo-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                    </svg>
                    {{ cliente.totalPedidos }} pedido(s)
                  </span>
                </td>

                <td class="py-3.5 px-4 text-center">
                  <span :class="[
                    'px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-xs',
                    cliente.estadoCliente 
                      ? 'bg-emerald-50 text-emerald-700 border-emerald-300' 
                      : 'bg-rose-50 text-rose-700 border-rose-300'
                  ]">
                    {{ cliente.estadoCliente ? 'Activo' : 'Inactivo' }}
                  </span>
                </td>

                <td class="py-3.5 px-5 text-right">
                  <div class="flex items-center justify-end gap-1.5">
                    <button
                      v-if="can('CLIENTES', 'modificar')"
                      @click="abrirModalEditar(cliente)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-slate-700 bg-slate-100 hover:bg-purple-600 hover:text-white border border-slate-200 transition-all shadow-xs"
                    >
                      Editar
                    </button>
                    <button
                      v-if="can('CLIENTES', 'eliminar')"
                      @click="solicitarToggleEstado(cliente)"
                      :class="[
                        'px-2.5 py-1.5 rounded-lg text-xs font-bold transition-all border shadow-xs',
                        cliente.estadoCliente
                          ? 'bg-rose-50 hover:bg-rose-600 text-rose-700 hover:text-white border-rose-200'
                          : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-700 hover:text-white border-emerald-200'
                      ]"
                    >
                      {{ cliente.estadoCliente ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Formulario (Crear / Editar) -->
    <Transition name="fade">
      <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden border border-slate-100 animate-scale-in">
          <div class="px-6 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">
                {{ isEditing ? 'Editar Cliente' : 'Registrar Nuevo Cliente' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Ingresa los datos comerciales y de contacto</p>
            </div>
            <button @click="showModal = false" :disabled="isSaving" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <form @submit.prevent="solicitarGuardar">
            <div class="px-6 py-5 space-y-4 max-h-[75vh] overflow-y-auto">
              <div v-if="erroresForm.general" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-bold rounded-xl px-4 py-3">
                {{ erroresForm.general }}
              </div>

              <!-- Nombre Cliente -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Nombre Comercial / Razón Social <span class="text-rose-500">*</span>
                </label>
                <input
                  v-model="formCliente.nombreCliente"
                  @keypress="allowOnly.nombreInput($event)"
                  required
                  type="text"
                  placeholder="Ej: Distribuidora Textil Moderna, S.A."
                  maxlength="150"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                />
                <p v-if="erroresForm.nombreCliente" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.nombreCliente }}
                </p>
              </div>

              <!-- Teléfono -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Teléfono de Contacto
                </label>
                <div class="relative">
                  <input
                    v-model="formCliente.telefonoCliente"
                    @keypress="allowOnly.telefonoInput($event)"
                    type="text"
                    placeholder="Ej: +502 2456-7890"
                    maxlength="20"
                    class="w-full pl-9 pr-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 font-mono placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                  />
                  <svg class="w-4 h-4 text-slate-400 absolute left-3 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                  </svg>
                </div>
                <p v-if="erroresForm.telefonoCliente" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.telefonoCliente }}
                </p>
              </div>

              <!-- Dirección -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Dirección de Entrega / Facturación
                </label>
                <textarea
                  v-model="formCliente.direccionCliente"
                  @keypress="allowOnly.descripcionInput($event)"
                  rows="3"
                  placeholder="Ej: 4ta Avenida 12-45, Zona 10, Ciudad de Guatemala"
                  maxlength="250"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                ></textarea>
                <div class="flex justify-between items-center mt-1">
                  <p v-if="erroresForm.direccionCliente" class="text-rose-600 text-[11px] font-bold">
                    {{ erroresForm.direccionCliente }}
                  </p>
                  <span class="text-[10px] font-mono text-slate-400 ml-auto">
                    {{ formCliente.direccionCliente?.length || 0 }} / 250
                  </span>
                </div>
              </div>
            </div>

            <!-- Footer Modal Formulario -->
            <div class="px-6 py-4 border-t border-slate-100 flex items-center justify-end gap-2.5 bg-slate-50/50">
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
                {{ isSaving ? 'Guardando...' : (isEditing ? 'Guardar Cambios' : 'Registrar Cliente') }}
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