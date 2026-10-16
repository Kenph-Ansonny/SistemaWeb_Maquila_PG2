<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import rolService from '../services/rolService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, TextRules } from '../utils/validators'

const { can } = usePermissions()

// ---------- Estados Principales ----------
const roles = ref([])
const permisosModulo = ref([])
const permisosOriginales = ref([])
const rolSeleccionado = ref(null)

const isLoadingRoles = ref(true)
const isLoadingPermisos = ref(false)
const isSaving = ref(false)

const mostrarInactivosRoles = ref(false)
const busquedaRol = ref('')

// ---------- Modales de Formulario Rol ----------
const showModalRol = ref(false)
const isEditingRol = ref(false)
const rolOriginalForm = ref(null)

const formRol = reactive({
  nombreRol: '',
  descripcion: ''
})
const erroresRol = reactive({})

// Modal de confirmación para guardar cambios (Diff de Rol o Permisos)
const showConfirmModal = ref(false)
const confirmModalConfig = reactive({
  title: '',
  subtitle: '',
  type: 'primary',
  detalles: [],
  onConfirm: null
})

// Diálogo Pop-in genérico (para cambios de estado o descartes)
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

// ---------- Filtros Computados ----------
const rolesFiltrados = computed(() => {
  return roles.value.filter(rol => {
    if (!mostrarInactivosRoles.value && !rol.estadoRol) return false
    if (busquedaRol.value.trim()) {
      const q = busquedaRol.value.toLowerCase().trim()
      return rol.nombreRol.toLowerCase().includes(q) || (rol.descripcion && rol.descripcion.toLowerCase().includes(q))
    }
    return true
  })
})

// Detección de cambios pendientes en la matriz
const tieneCambiosEnPermisos = computed(() => {
  if (permisosModulo.value.length === 0 || permisosOriginales.value.length === 0) return false
  return JSON.stringify(permisosModulo.value) !== JSON.stringify(permisosOriginales.value)
})

// Estadísticas de la matriz activa
const statsMatriz = computed(() => {
  const total = permisosModulo.value.length
  const conAcceso = permisosModulo.value.filter(p => p.puedeConsultar).length
  const fullAccess = permisosModulo.value.filter(p => p.puedeConsultar && p.puedeInsertar && p.puedeModificar && p.puedeEliminar).length
  return { total, conAcceso, fullAccess }
})

// ---------- Carga de Roles y Permisos ----------
const cargarRoles = async () => {
  if (!can('ROLES', 'consultar')) {
    isLoadingRoles.value = false
    return
  }

  isLoadingRoles.value = true
  try {
    const data = await rolService.obtenerRoles()
    roles.value = Array.isArray(data) ? data : (data?.data || [])

    // Mantener rol seleccionado o fijar el primero disponible
    if (rolesFiltrados.value.length > 0) {
      const actual = rolSeleccionado.value ? rolesFiltrados.value.find(r => r.idRol === rolSeleccionado.value.idRol) : null
      seleccionarRol(actual || rolesFiltrados.value[0])
    }
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudieron cargar los roles del sistema.')
  } finally {
    isLoadingRoles.value = false
  }
}

const seleccionarRol = async (rol) => {
  if (tieneCambiosEnPermisos.value) {
    const confirmar = await pedirConfirmacion({
      variant: 'warning',
      title: '¿Descartar cambios no guardados?',
      message: `Tienes modificaciones pendientes en la matriz del rol "${rolSeleccionado.value?.nombreRol}". Si continúas, se perderán estos privilegios editados.`,
      confirmText: 'Sí, cambiar de rol',
      cancelText: 'Permanecer aquí'
    })
    if (!confirmar) return
  }

  rolSeleccionado.value = rol
  isLoadingPermisos.value = true
  try {
    const data = await rolService.obtenerPermisos(rol.idRol)
    const lista = Array.isArray(data) ? data : (data?.data || [])
    permisosModulo.value = JSON.parse(JSON.stringify(lista))
    permisosOriginales.value = JSON.parse(JSON.stringify(lista))
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'Error al obtener la matriz de permisos.')
  } finally {
    isLoadingPermisos.value = false
  }
}

// ---------- Gestión de Permisos por Fila y Columna ----------
const estaFilaCompleta = (item) => {
  return item.puedeConsultar && item.puedeInsertar && item.puedeModificar && item.puedeEliminar
}

const toggleFilaCompleta = (item) => {
  const activo = estaFilaCompleta(item)
  const nuevoValor = !activo
  item.puedeConsultar = nuevoValor
  item.puedeInsertar = nuevoValor
  item.puedeModificar = nuevoValor
  item.puedeEliminar = nuevoValor
}

const onCheckboxIndividualChange = (item, tipo) => {
  // Regla lógica: si se otorga insertar, modificar o eliminar, obligatoriamente debe poder consultar
  if ((tipo === 'puedeInsertar' || tipo === 'puedeModificar' || tipo === 'puedeEliminar') && item[tipo]) {
    item.puedeConsultar = true
  }
  // Si apaga consultar, se apagan las operaciones derivadas
  if (tipo === 'puedeConsultar' && !item.puedeConsultar) {
    item.puedeInsertar = false
    item.puedeModificar = false
    item.puedeEliminar = false
  }
}

const togglePermisoColumna = (tipo) => {
  const allActive = permisosModulo.value.every(p => p[tipo])
  const nuevoValor = !allActive

  permisosModulo.value.forEach(p => {
    p[tipo] = nuevoValor
    if (nuevoValor && tipo !== 'puedeConsultar') {
      p.puedeConsultar = true
    }
    if (!nuevoValor && tipo === 'puedeConsultar') {
      p.puedeInsertar = false
      p.puedeModificar = false
      p.puedeEliminar = false
    }
  })
}

const marcarTodoElSistema = () => {
  const allFull = permisosModulo.value.every(estaFilaCompleta)
  const nuevoValor = !allFull

  permisosModulo.value.forEach(p => {
    p.puedeConsultar = nuevoValor
    p.puedeInsertar = nuevoValor
    p.puedeModificar = nuevoValor
    p.puedeEliminar = nuevoValor
  })
}

const descartarCambiosPermisos = () => {
  permisosModulo.value = JSON.parse(JSON.stringify(permisosOriginales.value))
  mostrarToast('warning', 'Se restablecieron los permisos guardados.')
}

// ---------- Diff y Guardado de la Matriz ----------
const prepararGuardadoPermisos = () => {
  if (!rolSeleccionado.value || !can('ROLES', 'modificar')) return

  if (!tieneCambiosEnPermisos.value) {
    mostrarToast('warning', 'No has realizado ninguna modificación en los permisos de este rol.')
    return
  }

  // Generar Diff de cambios
  const diferencias = []
  permisosModulo.value.forEach(pActual => {
    const pOrig = permisosOriginales.value.find(p => p.idModulo === pActual.idModulo)
    if (!pOrig) return

    const agregados = []
    const removidos = []

    const evaluar = (key, label) => {
      if (!pOrig[key] && pActual[key]) agregados.push(label)
      if (pOrig[key] && !pActual[key]) removidos.push(label)
    }

    evaluar('puedeConsultar', 'Consultar')
    evaluar('puedeInsertar', 'Insertar')
    evaluar('puedeModificar', 'Modificar')
    evaluar('puedeEliminar', 'Desactivar')

    if (agregados.length > 0 || removidos.length > 0) {
      diferencias.push({
        modulo: pActual.nombreModulo,
        codigo: pActual.codigoModulo,
        agregados,
        removidos
      })
    }
  })

  confirmModalConfig.title = '¿Confirmar actualización de privilegios?'
  confirmModalConfig.subtitle = `Se aplicarán ${diferencias.length} cambio(s) en la matriz para el perfil "${rolSeleccionado.value.nombreRol}".`
  confirmModalConfig.type = 'matriz'
  confirmModalConfig.detalles = diferencias
  confirmModalConfig.onConfirm = ejecutarGuardadoPermisos
  showConfirmModal.value = true
}

const ejecutarGuardadoPermisos = async () => {
  if (isSaving.value) return
  isSaving.value = true

  try {
    await rolService.guardarPermisos(rolSeleccionado.value.idRol, permisosModulo.value)
    permisosOriginales.value = JSON.parse(JSON.stringify(permisosModulo.value))
    showConfirmModal.value = false
    mostrarToast('success', `Matriz de permisos actualizada exitosamente para "${rolSeleccionado.value.nombreRol}".`)
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'Error al persistir la matriz de permisos.')
  } finally {
    isSaving.value = false
  }
}

// ---------- Formularios Crear / Editar Rol ----------
const limpiarErroresRol = () => {
  Object.keys(erroresRol).forEach(k => delete erroresRol[k])
}

const abrirModalCrearRol = () => {
  if (!can('ROLES', 'insertar')) return
  isEditingRol.value = false
  rolOriginalForm.value = null
  limpiarErroresRol()
  formRol.nombreRol = ''
  formRol.descripcion = ''
  showModalRol.value = true
}

const abrirModalEditarRol = (rol) => {
  if (!can('ROLES', 'modificar')) return
  isEditingRol.value = true
  rolOriginalForm.value = { nombreRol: rol.nombreRol, descripcion: rol.descripcion || '' }
  limpiarErroresRol()
  formRol.nombreRol = rol.nombreRol
  formRol.descripcion = rol.descripcion || ''
  showModalRol.value = true
}

const validarFormularioRol = () => {
  limpiarErroresRol()
  const nom = formRol.nombreRol?.trim() || ''
  const desc = formRol.descripcion?.trim() || ''

  if (!nom) {
    erroresRol.nombreRol = 'El nombre del rol es obligatorio.'
  } else if (!TextRules.esNombreValido(nom, 3, 50)) {
    erroresRol.nombreRol = 'El nombre debe tener entre 3 y 50 caracteres alfanuméricos válidos.'
  }

  if (desc && !TextRules.esDescripcionValida(desc, 200)) {
    erroresRol.descripcion = 'La descripción supera los 200 caracteres o contiene caracteres inválidos.'
  }

  return Object.keys(erroresRol).length === 0
}

const solicitarGuardarRol = () => {
  if (!validarFormularioRol()) return

  if (isEditingRol.value) {
    const nomActual = formRol.nombreRol.trim()
    const descActual = formRol.descripcion.trim()
    const nomAnt = rolOriginalForm.value.nombreRol.trim()
    const descAnt = rolOriginalForm.value.descripcion.trim()

    if (nomActual === nomAnt && descActual === descAnt) {
      mostrarToast('warning', 'No se detectaron modificaciones en el rol.')
      return
    }

    confirmModalConfig.title = '¿Guardar cambios en el rol?'
    confirmModalConfig.subtitle = 'Revisa las especificaciones modificadas antes de actualizar.'
    confirmModalConfig.type = 'rol_diff'
    confirmModalConfig.detalles = [
      { etiqueta: 'Nombre de Rol', anterior: nomAnt, nuevo: nomActual, haCambiado: nomActual !== nomAnt },
      { etiqueta: 'Descripción', anterior: descAnt || 'Sin descripción', nuevo: descActual || 'Sin descripción', haCambiado: descActual !== descAnt }
    ]
  } else {
    confirmModalConfig.title = '¿Registrar nuevo rol?'
    confirmModalConfig.subtitle = 'Se creará el perfil de seguridad con privilegios deshabilitados por defecto[cite: 1].'
    confirmModalConfig.type = 'rol_insert'
    confirmModalConfig.detalles = [
      { etiqueta: 'Nombre del Rol', nuevo: formRol.nombreRol.trim() },
      { etiqueta: 'Descripción', nuevo: formRol.descripcion.trim() || 'Sin descripción' }
    ]
  }

  confirmModalConfig.onConfirm = ejecutarGuardadoRol
  showConfirmModal.value = true
}

const ejecutarGuardadoRol = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const payload = {
    nombreRol: formRol.nombreRol.trim(),
    descripcion: formRol.descripcion.trim() || null
  }

  try {
    if (isEditingRol.value) {
      await rolService.actualizar(rolSeleccionado.value.idRol, payload)
      mostrarToast('success', `Rol "${payload.nombreRol}" actualizado exitosamente.`)
    } else {
      const res = await rolService.crear(payload)
      mostrarToast('success', `Rol "${payload.nombreRol}" registrado en el sistema.`)
      if (res?.idRol) {
        rolSeleccionado.value = { idRol: res.idRol }
      }
    }
    showConfirmModal.value = false
    showModalRol.value = false
    await cargarRoles()
  } catch (err) {
    showConfirmModal.value = false
    erroresRol.general = err?.response?.data?.message || 'Error al procesar el rol.'
    mostrarToast('error', erroresRol.general)
  } finally {
    isSaving.value = false
  }
}

// ---------- Toggle Estado de Rol ----------
const solicitarToggleEstado = async (rol) => {
  if (!can('ROLES', 'eliminar')) return
  if (rol.idRol === 1) {
    mostrarToast('warning', 'El rol principal de Administrador no puede ser inhabilitado.')
    return
  }

  const accion = rol.estadoRol ? 'desactivar' : 'activar'
  const advertenciaUsuarios = rol.cantidadUsuarios > 0 && rol.estadoRol
    ? `\n\nAtención: Este perfil tiene ${rol.cantidadUsuarios} usuario(s) asignado(s). Al desactivarlo, sus sesiones no podrán ejecutar acciones restringidas.`
    : ''

  const confirmado = await pedirConfirmacion({
    variant: rol.estadoRol ? 'danger' : 'primary',
    title: `¿${accion.charAt(0).toUpperCase() + accion.slice(1)} Rol?`,
    message: `¿Estás seguro de que deseas ${accion} el perfil "${rol.nombreRol}"?${advertenciaUsuarios}`,
    detalles: [
      `Rol: ${rol.nombreRol}`,
      `Usuarios asignados: ${rol.cantidadUsuarios}`,
      `Estado final: ${rol.estadoRol ? 'Inactivo' : 'Activo'}`
    ],
    confirmText: `Sí, ${accion}`,
    cancelText: 'Cancelar'
  })

  if (!confirmado) return

  try {
    const res = await rolService.cambiarEstado(rol.idRol)
    mostrarToast('success', res?.message || `Rol ${accion}do exitosamente.`)
    await cargarRoles()
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo alternar el estado del rol.')
  }
}

// Control Teclado (Escape)
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) resolverConfirmacion(false)
  else if (showConfirmModal.value) showConfirmModal.value = false
  else if (showModalRol.value) showModalRol.value = false
}

onMounted(() => {
  window.addEventListener('keydown', onKeydown)
  cargarRoles()
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
        <div class="bg-white rounded-3xl p-6 sm:p-7 shadow-2xl w-full max-w-lg border border-slate-100 text-center animate-scale-in max-h-[90vh] flex flex-col">
          <div class="w-16 h-16 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center mx-auto mb-4 border border-purple-100 shadow-sm shrink-0">
            <svg class="w-8 h-8 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
            </svg>
          </div>

          <h3 class="text-lg font-black text-slate-800 tracking-tight shrink-0">{{ confirmModalConfig.title }}</h3>
          <p class="text-xs text-slate-500 font-medium mt-1 mb-4 shrink-0">{{ confirmModalConfig.subtitle }}</p>

          <!-- Vista 1: Diff de Matriz de Permisos -->
          <div v-if="confirmModalConfig.type === 'matriz'" class="flex-1 overflow-y-auto pr-1 text-left space-y-2.5 mb-5 bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
            <div
              v-for="item in confirmModalConfig.detalles"
              :key="item.codigo"
              class="pb-2.5 last:pb-0 border-b border-slate-200/70 last:border-0"
            >
              <div class="flex items-center justify-between">
                <span class="text-xs font-black text-slate-800">{{ item.modulo }}</span>
                <span class="text-[10px] font-mono font-bold text-slate-400 bg-white px-1.5 py-0.5 rounded border border-slate-200">
                  {{ item.codigo }}
                </span>
              </div>

              <!-- Privilegios Otorgados -->
              <div v-if="item.agregados.length > 0" class="flex flex-wrap items-center gap-1.5 mt-1.5">
                <span class="text-[10px] font-bold text-emerald-700 uppercase">Habilitados:</span>
                <span
                  v-for="p in item.agregados"
                  :key="p"
                  class="px-2 py-0.5 rounded-md text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-300"
                >
                  + {{ p }}
                </span>
              </div>

              <!-- Privilegios Revocados -->
              <div v-if="item.removidos.length > 0" class="flex flex-wrap items-center gap-1.5 mt-1.5">
                <span class="text-[10px] font-bold text-rose-700 uppercase">Revocados:</span>
                <span
                  v-for="p in item.removidos"
                  :key="p"
                  class="px-2 py-0.5 rounded-md text-[10px] font-bold bg-rose-100 text-rose-800 border border-rose-300 line-through"
                >
                  - {{ p }}
                </span>
              </div>
            </div>
          </div>

          <!-- Vista 2: Diff de Rol (Modificación) -->
          <div v-else-if="confirmModalConfig.type === 'rol_diff'" class="flex-1 overflow-y-auto text-left space-y-3 mb-5 bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
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

          <!-- Vista 3: Resumen Inserción Rol -->
          <div v-else class="flex-1 overflow-y-auto text-left space-y-2.5 mb-5 bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
            <div v-for="d in confirmModalConfig.detalles" :key="d.etiqueta" class="text-xs flex justify-between items-center pb-2 border-b border-slate-200/70 last:border-0">
              <span class="font-bold text-slate-500 uppercase text-[10px]">{{ d.etiqueta }}:</span>
              <span class="font-bold text-slate-800">{{ d.nuevo }}</span>
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

    <!-- Diálogo de Confirmación Reutilizable (Pop-in) -->
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
            <div :class="['w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4', CONFIRM_VARIANTS[confirmDialog.variant]?.iconWrap || 'bg-purple-100 text-purple-600 ring-8 ring-purple-50']">
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
    <div v-if="!can('ROLES', 'consultar')" class="bg-white rounded-3xl p-14 text-center shadow-md border border-slate-100">
      <div class="w-16 h-16 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4 ring-8 ring-rose-50/60">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>
      </div>
      <h3 class="text-base font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-sm text-slate-500 mt-1.5 max-w-sm mx-auto">No cuentas con autorización para consultar el módulo de roles y seguridad.</p>
    </div>

    <!-- Layout Split-View Principal -->
    <div v-else class="space-y-5">
      <!-- Banner Informativo -->
      <div class="bg-gradient-to-r from-purple-50 via-white to-pink-50 border border-purple-200/70 rounded-2xl p-4 flex items-center justify-between gap-4 shadow-sm">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-purple-100 text-purple-700 flex items-center justify-center shrink-0 border border-purple-200 font-bold">
            🛡️
          </div>
          <div>
            <h4 class="text-xs font-black text-slate-800 uppercase tracking-wide">Control de Seguridad y Privilegios</h4>
            <p class="text-[11px] text-slate-600 font-medium mt-0.5">
              Los cambios en la matriz aplican al instante para nuevos tokens. Puedes seleccionar filas o columnas completas para configurar accesos rápidamente.
            </p>
          </div>
        </div>
        <div class="hidden md:flex items-center gap-2 font-mono text-xs font-bold text-purple-800 bg-white/80 px-3 py-1.5 rounded-xl border border-purple-200 shrink-0">
          <span>Roles: {{ roles.length }}</span>
          <span>·</span>
          <span>Módulos: {{ statsMatriz.total }}</span>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <!-- Columna Izquierda: Selector de Roles -->
        <div class="lg:col-span-4 bg-white rounded-3xl p-5 shadow-md border border-slate-100 space-y-4">
          <div class="flex items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <h2 class="text-sm font-black text-slate-800">Perfiles del Sistema</h2>
              <p class="text-[11px] text-slate-400 font-semibold mt-0.5">Selecciona para configurar privilegios</p>
            </div>
            <button 
              v-if="can('ROLES', 'insertar')" 
              @click="abrirModalCrearRol" 
              class="px-3.5 py-1.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-bold text-xs rounded-xl shadow-md shadow-purple-500/25 hover:shadow-lg hover:scale-[1.02] active:scale-[0.98] transition-all flex items-center gap-1.5"
            >
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" /></svg>
              Nuevo Rol
            </button>
          </div>

          <!-- Filtros de Roles -->
          <div class="space-y-2.5">
            <div class="relative">
              <input
                v-model="busquedaRol"
                type="text"
                placeholder="Filtrar rol por nombre..."
                class="w-full pl-8 pr-3 py-1.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-700 placeholder-slate-400 focus:bg-white focus:outline-none focus:border-purple-500 transition"
              />
              <svg class="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" /></svg>
            </div>

            <label class="flex items-center gap-2 text-xs font-bold text-slate-600 cursor-pointer select-none px-1">
              <input 
                type="checkbox" 
                v-model="mostrarInactivosRoles" 
                class="rounded border-slate-300 text-purple-600 focus:ring-purple-500"
              />
              <span>Mostrar roles inactivos</span>
            </label>
          </div>

          <!-- Listado de Roles con Alta Visibilidad al Seleccionar -->
          <div class="space-y-3 max-h-[580px] overflow-y-auto pr-1">
            <div 
              v-for="rol in rolesFiltrados" 
              :key="rol.idRol"
              @click="seleccionarRol(rol)"
              :class="[
                'relative overflow-hidden rounded-2xl border-2 transition-all cursor-pointer p-4',
                rolSeleccionado?.idRol === rol.idRol 
                  ? 'border-purple-600 bg-purple-50/70 shadow-lg shadow-purple-500/15 ring-2 ring-purple-600/20 translate-x-1' 
                  : 'border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50/70 hover:shadow-sm'
              ]"
            >
              <!-- Barra de acento izquierda de alta visibilidad para el rol activo -->
              <div
                v-if="rolSeleccionado?.idRol === rol.idRol"
                class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"
              ></div>

              <div class="pl-1 space-y-2">
                <div class="flex items-start justify-between gap-2">
                  <div>
                    <div class="flex items-center gap-1.5">
                      <span :class="['font-black text-sm', rolSeleccionado?.idRol === rol.idRol ? 'text-purple-950 font-bold' : 'text-slate-800']">
                        {{ rol.nombreRol }}
                      </span>
                      <span v-if="rol.idRol === 1" class="text-[9px] font-black uppercase tracking-wider px-1.5 py-0.5 rounded bg-purple-200/80 text-purple-800">
                        Admin
                      </span>
                    </div>
                    <p class="text-xs text-slate-500 font-medium line-clamp-2 mt-0.5">
                      {{ rol.descripcion || 'Sin descripción asignada' }}
                    </p>
                  </div>

                  <span :class="[
                    'px-2.5 py-0.5 rounded-full text-[10px] font-black uppercase tracking-wider border shrink-0',
                    rol.estadoRol ? 'bg-emerald-50 text-emerald-700 border-emerald-300' : 'bg-rose-50 text-rose-700 border-rose-300'
                  ]">
                    {{ rol.estadoRol ? 'Activo' : 'Inactivo' }}
                  </span>
                </div>

                <div class="flex items-center justify-between pt-1">
                  <span :class="['text-xs font-bold font-mono', rolSeleccionado?.idRol === rol.idRol ? 'text-purple-700 font-black' : 'text-slate-500']">
                    👥 {{ rol.cantidadUsuarios }} usuario(s)
                  </span>

                  <!-- Botones de Acción con Alto Contraste -->
                  <div class="flex items-center gap-1.5" @click.stop>
                    <button 
                      v-if="can('ROLES', 'modificar')" 
                      @click="abrirModalEditarRol(rol)" 
                      class="px-2.5 py-1 bg-white hover:bg-purple-600 hover:text-white text-slate-700 rounded-lg text-xs font-bold transition border border-slate-300 shadow-2xs"
                      title="Editar nombre y descripción"
                    >
                      Editar
                    </button>

                    <button 
                      v-if="can('ROLES', 'eliminar') && rol.idRol !== 1" 
                      @click="solicitarToggleEstado(rol)" 
                      :class="[
                        'px-2.5 py-1 rounded-lg text-xs font-bold transition border shadow-2xs',
                        rol.estadoRol 
                          ? 'bg-rose-50 hover:bg-rose-600 text-rose-700 hover:text-white border-rose-300' 
                          : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-700 hover:text-white border-emerald-300'
                      ]"
                    >
                      {{ rol.estadoRol ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <div v-if="rolesFiltrados.length === 0 && !isLoadingRoles" class="py-8 text-center text-slate-400 text-xs italic">
              No se encontraron roles coincidentes.
            </div>
          </div>
        </div>

        <!-- Columna Derecha: Matriz Granular de Permisos -->
        <div class="lg:col-span-8 bg-white rounded-3xl p-6 shadow-md border border-slate-100 space-y-4">
          <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between pb-4 border-b border-slate-100 gap-3">
            <div>
              <div class="flex items-center gap-2">
                <h2 class="text-base font-black text-slate-900 tracking-tight">
                  Matriz: {{ rolSeleccionado?.nombreRol || 'Selecciona un rol' }}
                </h2>
                <span v-if="tieneCambiosEnPermisos" class="text-[10px] font-bold px-2 py-0.5 rounded-full bg-amber-100 text-amber-800 border border-amber-300 animate-pulse">
                  Cambios pendientes
                </span>
              </div>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">
                Acceso a pantallas: <b class="text-purple-700 font-mono">{{ statsMatriz.conAcceso }} de {{ statsMatriz.total }}</b> módulos habilitados
              </p>
            </div>

            <div class="flex items-center gap-2 self-end sm:self-auto">
              <button 
                type="button"
                @click="descartarCambiosPermisos" 
                :disabled="!tieneCambiosEnPermisos || isSaving"
                class="px-3.5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl text-xs font-bold transition disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Descartar
              </button>
              <button 
                v-if="can('ROLES', 'modificar')" 
                type="button"
                @click="prepararGuardadoPermisos" 
                :disabled="!tieneCambiosEnPermisos || isSaving"
                class="px-4 py-2 bg-gradient-to-tl from-purple-700 to-pink-500 text-white font-bold text-xs rounded-xl shadow-md shadow-purple-500/25 hover:shadow-lg transition-all flex items-center gap-1.5 disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4" /></svg>
                Guardar Permisos
              </button>
            </div>
          </div>

          <!-- Barra de Acciones Globales Rápidas -->
          <div class="flex flex-wrap items-center justify-between gap-2 p-2.5 bg-slate-50 border border-slate-200/80 rounded-2xl text-xs">
            <span class="text-[11px] font-bold text-slate-500 uppercase tracking-wider pl-1">Acciones Rápidas:</span>
            <div class="flex flex-wrap items-center gap-1.5">
              <button
                type="button"
                @click="marcarTodoElSistema"
                class="px-2.5 py-1 text-[11px] font-bold text-purple-700 bg-white hover:bg-purple-100/70 border border-purple-200 rounded-lg transition shadow-2xs"
              >
                ⚡ Marcar Todo el Sistema
              </button>
            </div>
          </div>

          <!-- Tabla de la Matriz con Selección Rápida por Fila y Columna -->
          <div class="overflow-x-auto border border-slate-100 rounded-2xl">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="bg-slate-50/80 border-b border-slate-200 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                  <th class="py-3.5 px-4 min-w-[200px]">Módulo / Pantalla</th>
                  <th class="py-3.5 px-3 text-center whitespace-nowrap">Fila Completa</th>
                  <th class="py-3.5 px-3 text-center cursor-pointer hover:text-purple-700 select-none transition whitespace-nowrap" @click="togglePermisoColumna('puedeConsultar')" title="Clic para alternar toda la columna">
                    Consultar ⇅
                  </th>
                  <th class="py-3.5 px-3 text-center cursor-pointer hover:text-purple-700 select-none transition whitespace-nowrap" @click="togglePermisoColumna('puedeInsertar')" title="Clic para alternar toda la columna">
                    Insertar ⇅
                  </th>
                  <th class="py-3.5 px-3 text-center cursor-pointer hover:text-purple-700 select-none transition whitespace-nowrap" @click="togglePermisoColumna('puedeModificar')" title="Clic para alternar toda la columna">
                    Modificar ⇅
                  </th>
                  <th class="py-3.5 px-3 text-center cursor-pointer hover:text-purple-700 select-none transition whitespace-nowrap" @click="togglePermisoColumna('puedeEliminar')" title="Clic para alternar toda la columna">
                    Desactivar ⇅
                  </th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-xs">
                <tr v-if="isLoadingPermisos">
                  <td colspan="6" class="py-14 text-center text-slate-400 font-semibold">
                    Cargando privilegios del rol...
                  </td>
                </tr>

                <tr 
                  v-for="item in permisosModulo" 
                  :key="item.idModulo"
                  :class="[
                    'hover:bg-purple-50/30 transition-colors',
                    estaFilaCompleta(item) ? 'bg-purple-50/20' : ''
                  ]"
                >
                  <td class="py-3 px-4 font-bold text-slate-800">
                    <span class="block text-slate-900 leading-tight">{{ item.nombreModulo }}</span>
                    <span class="text-[10px] text-purple-700 font-mono font-bold">{{ item.codigoModulo }}</span>
                  </td>

                  <!-- Botón / Switch de Fila Completa -->
                  <td class="py-3 px-3 text-center">
                    <button
                      type="button"
                      @click="toggleFilaCompleta(item)"
                      :class="[
                        'px-2.5 py-1 text-[10px] font-black uppercase tracking-wider rounded-lg border transition shadow-2xs whitespace-nowrap',
                        estaFilaCompleta(item)
                          ? 'bg-purple-600 text-white border-purple-700 hover:bg-purple-700'
                          : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-100 hover:text-slate-900'
                      ]"
                      :title="estaFilaCompleta(item) ? 'Desmarcar todos los privilegios de este módulo' : 'Marcar todos los privilegios de este módulo'"
                    >
                      {{ estaFilaCompleta(item) ? '✓ Todos' : 'Marcar Fila' }}
                    </button>
                  </td>

                  <!-- Consultar -->
                  <td class="py-3 px-3 text-center">
                    <input 
                      type="checkbox" 
                      v-model="item.puedeConsultar" 
                      @change="onCheckboxIndividualChange(item, 'puedeConsultar')"
                      class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500 cursor-pointer" 
                    />
                  </td>

                  <!-- Insertar -->
                  <td class="py-3 px-3 text-center">
                    <input 
                      type="checkbox" 
                      v-model="item.puedeInsertar" 
                      @change="onCheckboxIndividualChange(item, 'puedeInsertar')"
                      class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500 cursor-pointer" 
                    />
                  </td>

                  <!-- Modificar -->
                  <td class="py-3 px-3 text-center">
                    <input 
                      type="checkbox" 
                      v-model="item.puedeModificar" 
                      @change="onCheckboxIndividualChange(item, 'puedeModificar')"
                      class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500 cursor-pointer" 
                    />
                  </td>

                  <!-- Desactivar / Eliminar -->
                  <td class="py-3 px-3 text-center">
                    <input 
                      type="checkbox" 
                      v-model="item.puedeEliminar" 
                      @change="onCheckboxIndividualChange(item, 'puedeEliminar')"
                      class="h-4 w-4 rounded border-slate-300 text-purple-600 focus:ring-purple-500 cursor-pointer" 
                    />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Formulario de Rol (Crear / Editar) -->
    <Transition name="fade">
      <div v-if="showModalRol" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm" role="dialog" aria-modal="true">
        <div class="modal-card bg-white rounded-3xl shadow-2xl w-full max-w-sm border border-slate-100 overflow-hidden animate-scale-in">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <div class="px-6 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">
                {{ isEditingRol ? 'Editar Perfil de Rol' : 'Crear Nuevo Rol' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Información general del perfil de seguridad[cite: 1]</p>
            </div>
            <button @click="showModalRol = false" :disabled="isSaving" class="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          <form @submit.prevent="solicitarGuardarRol" class="p-6 space-y-4">
            <div v-if="erroresRol.general" class="bg-rose-50 border border-rose-200 text-rose-700 text-xs font-bold rounded-xl px-4 py-3">
              {{ erroresRol.general }}
            </div>

            <div>
              <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                Nombre del Rol <span class="text-rose-500">*</span>
              </label>
              <input 
                v-model="formRol.nombreRol" 
                @keypress="allowOnly.nombreInput($event)" 
                required 
                maxlength="50" 
                type="text" 
                placeholder="Ej: Encargado de Bodega" 
                class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500" 
              />
              <p v-if="erroresRol.nombreRol" class="text-rose-600 text-[11px] font-bold mt-1">
                {{ erroresRol.nombreRol }}
              </p>
            </div>

            <div>
              <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                Descripción del Rol
              </label>
              <textarea 
                v-model="formRol.descripcion" 
                @keypress="allowOnly.descripcionInput($event)" 
                maxlength="200" 
                rows="3" 
                placeholder="Propósito del perfil, responsabilidades y alcances..." 
                class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500"
              ></textarea>
              <div class="flex justify-between items-center mt-1">
                <p v-if="erroresRol.descripcion" class="text-rose-600 text-[11px] font-bold">
                  {{ erroresRol.descripcion }}
                </p>
                <span class="text-[10px] font-mono text-slate-400 ml-auto">
                  {{ formRol.descripcion?.length || 0 }} / 200
                </span>
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
              <button 
                type="button" 
                @click="showModalRol = false" 
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
                {{ isSaving ? 'Guardando...' : (isEditingRol ? 'Revisar y Guardar' : 'Revisar y Crear') }}
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