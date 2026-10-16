<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import proveedorService from '../services/proveedorService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

// ---------- Estado Principal ----------
const proveedores = ref([])
const isLoading = ref(true)
const isSaving = ref(false)

const filtroBusqueda = ref('')
const filtroEstado = ref('') // '' = Todos, 'activos', 'inactivos'

// ---------- Modales ----------
const showModal = ref(false)
const isEditing = ref(false)
const currentId = ref(null)

const proveedorOriginal = ref(null)
const showConfirmGuardar = ref(false)

const formProveedor = reactive({
  nombreProveedor: '',
  contactoPrincipal: '',
  telefono: '',
  direccion: ''
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
  if (!isEditing.value || !proveedorOriginal.value) return []

  const nomActual = (formProveedor.nombreProveedor || '').trim()
  const nomAnt = (proveedorOriginal.value.nombreProveedor || '').trim()

  const conActual = (formProveedor.contactoPrincipal || '').trim()
  const conAnt = (proveedorOriginal.value.contactoPrincipal || '').trim()

  const telActual = (formProveedor.telefono || '').trim()
  const telAnt = (proveedorOriginal.value.telefono || '').trim()

  const dirActual = (formProveedor.direccion || '').trim()
  const dirAnt = (proveedorOriginal.value.direccion || '').trim()

  return [
    {
      campo: 'nombreProveedor',
      etiqueta: 'Proveedor / Razón Social',
      anterior: nomAnt,
      nuevo: nomActual,
      haCambiado: nomActual !== nomAnt
    },
    {
      campo: 'contactoPrincipal',
      etiqueta: 'Contacto Principal',
      anterior: conAnt || 'Sin contacto',
      nuevo: conActual || 'Sin contacto',
      haCambiado: conActual !== conAnt
    },
    {
      campo: 'telefono',
      etiqueta: 'Teléfono de Contacto',
      anterior: telAnt || 'Sin teléfono',
      nuevo: telActual || 'Sin teléfono',
      haCambiado: telActual !== telAnt
    },
    {
      campo: 'direccion',
      etiqueta: 'Dirección Fiscal / Bodega',
      anterior: dirAnt || 'Sin dirección',
      nuevo: dirActual || 'Sin dirección',
      haCambiado: dirActual !== dirAnt
    }
  ]
})

const cambiosDetectados = computed(() => resumenCambios.value.filter(c => c.haCambiado))

// ---------- KPIs ----------
const kpis = computed(() => {
  const lista = proveedores.value
  return {
    total: lista.length,
    activos: lista.filter(p => p.estadoProveedor).length,
    conContacto: lista.filter(p => p.contactoPrincipal && p.contactoPrincipal.trim()).length,
    inactivos: lista.filter(p => !p.estadoProveedor).length
  }
})

// ---------- Filtrado Reactivo ----------
const proveedoresFiltrados = computed(() => {
  return proveedores.value.filter(p => {
    if (filtroEstado.value === 'activos' && !p.estadoProveedor) return false
    if (filtroEstado.value === 'inactivos' && p.estadoProveedor) return false

    if (filtroBusqueda.value.trim()) {
      const term = filtroBusqueda.value.toLowerCase().trim()
      const matchNombre = p.nombreProveedor.toLowerCase().includes(term)
      const matchContacto = p.contactoPrincipal ? p.contactoPrincipal.toLowerCase().includes(term) : false
      const matchTelefono = p.telefono ? p.telefono.toLowerCase().includes(term) : false
      const matchDireccion = p.direccion ? p.direccion.toLowerCase().includes(term) : false
      return matchNombre || matchContacto || matchTelefono || matchDireccion
    }
    return true
  })
})

// ---------- Carga de Datos ----------
const cargarProveedores = async () => {
  if (!can('PROVEEDORES', 'consultar')) {
    isLoading.value = false
    return
  }

  isLoading.value = true
  try {
    const data = await proveedorService.obtenerTodos()
    proveedores.value = Array.isArray(data) ? data : (data?.data || [])
  } catch (err) {
    showAlert('Error de Comunicación', err?.response?.data?.message || 'No se pudo sincronizar el catálogo de proveedores.', 'error')
  } finally {
    isLoading.value = false
  }
}

// ---------- Apertura de Formularios ----------
const abrirModalCrear = () => {
  if (!can('PROVEEDORES', 'insertar')) return
  isEditing.value = false
  currentId.value = null
  proveedorOriginal.value = null
  formProveedor.nombreProveedor = ''
  formProveedor.contactoPrincipal = ''
  formProveedor.telefono = ''
  formProveedor.direccion = ''
  limpiarErrores()
  showModal.value = true
}

const abrirModalEditar = (proveedor) => {
  if (!can('PROVEEDORES', 'modificar')) return
  isEditing.value = true
  currentId.value = proveedor.idProveedor

  proveedorOriginal.value = {
    nombreProveedor: proveedor.nombreProveedor || '',
    contactoPrincipal: proveedor.contactoPrincipal || '',
    telefono: proveedor.telefono || '',
    direccion: proveedor.direccion || ''
  }

  formProveedor.nombreProveedor = proveedor.nombreProveedor || ''
  formProveedor.contactoPrincipal = proveedor.contactoPrincipal || ''
  formProveedor.telefono = proveedor.telefono || ''
  formProveedor.direccion = proveedor.direccion || ''

  limpiarErrores()
  showModal.value = true
}

const limpiarErrores = () => {
  Object.keys(erroresForm).forEach(k => delete erroresForm[k])
}

// ---------- Validaciones del Formulario ----------
const validarFormulario = () => {
  limpiarErrores()
  const nom = formProveedor.nombreProveedor?.trim() || ''
  const con = formProveedor.contactoPrincipal?.trim() || ''
  const tel = formProveedor.telefono?.trim() || ''
  const dir = formProveedor.direccion?.trim() || ''

  if (!nom) {
    erroresForm.nombreProveedor = 'El nombre o razón social del proveedor es obligatorio.'
  } else if (!TextRules.esNombreValido(nom, 3, 150)) {
    erroresForm.nombreProveedor = 'El nombre debe contener entre 3 y 150 caracteres válidos.'
  }

  if (con && con.length > 100) {
    erroresForm.contactoPrincipal = 'El nombre de contacto excede los 100 caracteres.'
  }

  if (tel && !TextRules.esTelefonoValido(tel)) {
    erroresForm.telefono = 'Formato no válido (números, +, guiones, espacios y paréntesis, 7-20 caracteres).'
  }

  if (dir && !TextRules.esDescripcionValida(dir, 200)) {
    erroresForm.direccion = 'La dirección excede la longitud máxima (200 caracteres).'
  }

  return Object.keys(erroresForm).length === 0
}

// ---------- Disparador de Guardado ----------
const solicitarGuardar = () => {
  if (!validarFormulario()) return

  if (isEditing.value) {
    if (cambiosDetectados.value.length === 0) {
      showAlert('Sin Cambios', 'No realizaste ninguna modificación en los datos del proveedor.', 'warning')
      return
    }
    showConfirmGuardar.value = true
  } else {
    ejecutarGuardado()
  }
}

// ---------- Guardar / Actualizar ----------
const ejecutarGuardado = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const payload = {
    nombreProveedor: formProveedor.nombreProveedor.trim(),
    contactoPrincipal: formProveedor.contactoPrincipal.trim() || null,
    telefono: formProveedor.telefono.trim() || null,
    direccion: formProveedor.direccion.trim() || null
  }

  try {
    if (isEditing.value) {
      await proveedorService.actualizar(currentId.value, payload)
      showAlert('Modificación Exitosa', 'El proveedor fue actualizado correctamente.')
    } else {
      await proveedorService.crear(payload)
      showAlert('Registro Exitoso', 'El proveedor fue registrado en el catálogo.')
    }
    showConfirmGuardar.value = false
    showModal.value = false
    await cargarProveedores()
  } catch (err) {
    showConfirmGuardar.value = false
    erroresForm.general = err?.response?.data?.message || 'Ocurrió un error al procesar la solicitud.'
  } finally {
    isSaving.value = false
  }
}

// ---------- Cambio de Estado ----------
const solicitarToggleEstado = (proveedor) => {
  if (!can('PROVEEDORES', 'eliminar')) return
  const accion = proveedor.estadoProveedor ? 'desactivar' : 'activar'
  confirmModal.value = {
    show: true,
    title: `¿${accion.charAt(0).toUpperCase() + accion.slice(1)} Proveedor?`,
    message: `¿Estás seguro de que deseas ${accion} al proveedor "${proveedor.nombreProveedor}"?`,
    type: proveedor.estadoProveedor ? 'danger' : 'success',
    action: async () => {
      confirmModal.value.show = false
      try {
        const res = await proveedorService.cambiarEstado(proveedor.idProveedor)
        showAlert('Estado Actualizado', res?.message || res?.data?.message || `Proveedor ${accion}do exitosamente.`)
        await cargarProveedores()
      } catch (err) {
        showAlert('Error', err?.response?.data?.message || 'No se pudo cambiar el estado del proveedor.', 'error')
      }
    }
  }
}

onMounted(cargarProveedores)
</script>

<template>
  <MainLayout>
    <!-- Modal Confirmación de Guardar Cambios (Diff Visual) -->
    <Transition name="fade">
      <div v-if="showConfirmGuardar" class="fixed inset-0 z-[65] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
        <div class="bg-white rounded-3xl p-6 sm:p-7 shadow-2xl w-full max-w-md border border-slate-100 text-center animate-scale-in">
          <div class="w-16 h-16 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center mx-auto mb-4 border border-purple-100 shadow-sm">
            <svg class="w-8 h-8 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>

          <h3 class="text-lg font-black text-slate-800 tracking-tight">¿Guardar los cambios?</h3>
          <p class="text-xs text-slate-500 font-medium mt-1 mb-5">
            Revisa los datos antes de actualizar el proveedor en el catálogo.
          </p>

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
              {{ isSaving ? 'Guardando...' : 'Sí, guardar cambios' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Modal Notificación Global -->
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
    <div v-if="!can('PROVEEDORES', 'consultar')" class="bg-white rounded-2xl p-12 text-center shadow-md border border-slate-100">
      <div class="w-14 h-14 rounded-2xl bg-rose-50 text-rose-600 flex items-center justify-center mx-auto mb-3 border border-rose-200 shadow-sm">
        <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
      </div>
      <h3 class="text-base font-black text-slate-800">Acceso Restringido</h3>
      <p class="text-xs text-slate-500 font-medium mt-1">No cuentas con autorización para consultar el catálogo de proveedores.</p>
    </div>

    <!-- Contenedor Principal -->
    <div v-else class="space-y-5">
      <!-- Encabezado con Botón -->
      <div class="bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Catálogo de Proveedores</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de proveedores de materias primas, telas, hilos y suministros de confección</p>
        </div>
        <button
          v-if="can('PROVEEDORES', 'insertar')"
          @click="abrirModalCrear"
          class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:scale-[1.01] active:scale-[0.98] transition-all"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
          </svg>
          Nuevo Proveedor
        </button>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Total Proveedores</p>
            <p class="text-2xl font-black text-slate-900 mt-1 font-mono">{{ kpis.total }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center border border-purple-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
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
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Con Contacto</p>
            <p class="text-2xl font-black text-indigo-600 mt-1 font-mono">{{ kpis.conContacto }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center border border-indigo-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
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
            placeholder="Buscar proveedor por nombre, contacto, teléfono o dirección..."
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
          <p class="text-xs font-bold text-slate-600 mt-3">Cargando catálogo de proveedores...</p>
        </div>

        <div v-else-if="proveedoresFiltrados.length === 0" class="text-center py-20 px-4">
          <div class="w-14 h-14 bg-purple-50 text-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-3 border border-purple-100">
            <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          </div>
          <p class="text-sm font-black text-slate-800">No se encontraron proveedores registrados</p>
          <p class="text-xs text-slate-500 font-medium mt-1">Ajusta los términos de búsqueda o registra un nuevo proveedor.</p>
        </div>

        <div v-else class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-slate-100 bg-slate-50/70 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                <th class="py-3.5 px-5">Proveedor / Empresa</th>
                <th class="py-3.5 px-4">Contacto Principal</th>
                <th class="py-3.5 px-4">Teléfono</th>
                <th class="py-3.5 px-4">Dirección Fiscal / Bodega</th>
                <th class="py-3.5 px-4 text-center">Estado</th>
                <th class="py-3.5 px-5 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr
                v-for="proveedor in proveedoresFiltrados"
                :key="proveedor.idProveedor"
                class="hover:bg-purple-50/20 transition-colors"
              >
                <td class="py-3.5 px-5">
                  <div class="flex items-center gap-3">
                    <div class="w-9 h-9 rounded-xl bg-purple-100/70 text-purple-700 flex items-center justify-center font-bold text-xs shrink-0 border border-purple-200">
                      {{ proveedor.nombreProveedor.charAt(0).toUpperCase() }}
                    </div>
                    <div>
                      <span class="block text-slate-900 font-bold text-[13px] leading-tight">
                        {{ proveedor.nombreProveedor }}
                      </span>
                      <span class="text-[10px] font-mono font-bold text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-200 inline-block mt-0.5">
                        ID: #{{ proveedor.idProveedor }}
                      </span>
                    </div>
                  </div>
                </td>

                <td class="py-3.5 px-4">
                  <span v-if="proveedor.contactoPrincipal" class="text-slate-800 font-semibold">
                    {{ proveedor.contactoPrincipal }}
                  </span>
                  <span v-else class="text-slate-400 font-mono text-xs">—</span>
                </td>

                <td class="py-3.5 px-4">
                  <div v-if="proveedor.telefono" class="flex items-center gap-1.5 text-slate-800 font-bold font-mono">
                    <svg class="w-3.5 h-3.5 text-emerald-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                    </svg>
                    <span>{{ proveedor.telefono }}</span>
                  </div>
                  <span v-else class="text-slate-400 font-mono text-xs">—</span>
                </td>

                <td class="py-3.5 px-4 max-w-xs">
                  <span v-if="proveedor.direccion" class="text-slate-700 font-medium line-clamp-2" :title="proveedor.direccion">
                    {{ proveedor.direccion }}
                  </span>
                  <span v-else class="text-slate-400 font-mono text-xs">—</span>
                </td>

                <td class="py-3.5 px-4 text-center">
                  <span :class="[
                    'px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-xs',
                    proveedor.estadoProveedor 
                      ? 'bg-emerald-50 text-emerald-700 border-emerald-300' 
                      : 'bg-rose-50 text-rose-700 border-rose-300'
                  ]">
                    {{ proveedor.estadoProveedor ? 'Activo' : 'Inactivo' }}
                  </span>
                </td>

                <td class="py-3.5 px-5 text-right">
                  <div class="flex items-center justify-end gap-1.5">
                    <button
                      v-if="can('PROVEEDORES', 'modificar')"
                      @click="abrirModalEditar(proveedor)"
                      class="px-2.5 py-1.5 rounded-lg text-xs font-bold text-slate-700 bg-slate-100 hover:bg-purple-600 hover:text-white border border-slate-200 transition-all shadow-xs"
                    >
                      Editar
                    </button>
                    <button
                      v-if="can('PROVEEDORES', 'eliminar')"
                      @click="solicitarToggleEstado(proveedor)"
                      :class="[
                        'px-2.5 py-1.5 rounded-lg text-xs font-bold transition-all border shadow-xs',
                        proveedor.estadoProveedor
                          ? 'bg-rose-50 hover:bg-rose-600 text-rose-700 hover:text-white border-rose-200'
                          : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-700 hover:text-white border-emerald-200'
                      ]"
                    >
                      {{ proveedor.estadoProveedor ? 'Desactivar' : 'Activar' }}
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
                {{ isEditing ? 'Editar Proveedor' : 'Registrar Nuevo Proveedor' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Ingresa los datos de abastecimiento y contacto comercial</p>
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

              <!-- Nombre Proveedor -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Nombre de la Empresa / Proveedor <span class="text-rose-500">*</span>
                </label>
                <input
                  v-model="formProveedor.nombreProveedor"
                  @keypress="allowOnly.nombreInput($event)"
                  required
                  type="text"
                  placeholder="Ej: Textiles del Pacífico, S.A."
                  maxlength="150"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                />
                <p v-if="erroresForm.nombreProveedor" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.nombreProveedor }}
                </p>
              </div>

              <!-- Contacto Principal -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Contacto Principal / Asesor de Ventas
                </label>
                <input
                  v-model="formProveedor.contactoPrincipal"
                  @keypress="allowOnly.nombreInput($event)"
                  type="text"
                  placeholder="Ej: Lic. Carlos Morales"
                  maxlength="100"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                />
                <p v-if="erroresForm.contactoPrincipal" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.contactoPrincipal }}
                </p>
              </div>

              <!-- Teléfono -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Teléfono de Contacto
                </label>
                <div class="relative">
                  <input
                    v-model="formProveedor.telefono"
                    @keypress="allowOnly.telefonoInput($event)"
                    type="text"
                    placeholder="Ej: +502 2334-5566"
                    maxlength="30"
                    class="w-full pl-9 pr-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-800 font-mono placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                  />
                  <svg class="w-4 h-4 text-slate-400 absolute left-3 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                  </svg>
                </div>
                <p v-if="erroresForm.telefono" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.telefono }}
                </p>
              </div>

              <!-- Dirección -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Dirección Fiscal / Bodega
                </label>
                <textarea
                  v-model="formProveedor.direccion"
                  @keypress="allowOnly.descripcionInput($event)"
                  rows="3"
                  placeholder="Ej: Calzada San Juan 15-40, Zona 7, Parque Industrial El Trébol"
                  maxlength="200"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
                ></textarea>
                <div class="flex justify-between items-center mt-1">
                  <p v-if="erroresForm.direccion" class="text-rose-600 text-[11px] font-bold">
                    {{ erroresForm.direccion }}
                  </p>
                  <span class="text-[10px] font-mono text-slate-400 ml-auto">
                    {{ formProveedor.direccion?.length || 0 }} / 200
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
                {{ isSaving ? 'Guardando...' : (isEditing ? 'Guardar Cambios' : 'Registrar Proveedor') }}
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