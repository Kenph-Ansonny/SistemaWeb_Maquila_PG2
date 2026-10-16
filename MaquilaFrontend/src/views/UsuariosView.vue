<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import usuarioService from '../services/usuarioService'
import { usePermissions } from '../composables/usePermissions'
import { allowOnly, PasswordRules, EmailRules } from '../utils/validators'

const { can } = usePermissions()

// ---------- Variantes del Diálogo Pop-in (Indispensable para renderizar el modal) ----------
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
const usuarios = ref([])
const rolesDisponibles = ref([])
const isLoading = ref(true)
const isSaving = ref(false)

// Modales
const showModal = ref(false)
const isEditing = ref(false)
const currentUserId = ref(null)
const usuarioOriginal = ref(null)

// Filtros reactivos
const filtroBusqueda = ref('')
const filtroRol = ref('')
const mostrarInactivos = ref(false)

// Formulario de Usuario
const formUsuario = reactive({
  nombreUsuario: '',
  correo: '',
  password: '',
  rolesIds: []
})
const confirmPassword = ref('')
const showPassword = ref(false)
const erroresForm = reactive({})

// Modal de Confirmación Guardar (Resumen Insert / Diff Modificar)
const showConfirmGuardar = ref(false)

// Diálogo Pop-in genérico (para cambios de estado y bloqueos)
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

// ---------- Formateadores ----------
const formatFecha = (fechaStr) => {
  if (!fechaStr) return 'Nunca'
  const strUtc = fechaStr.endsWith('Z') ? fechaStr : `${fechaStr}Z`
  const d = new Date(strUtc)
  return d.toLocaleString('es-GT', {
    timeZone: 'America/Guatemala',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: true
  })
}

const obtenerNombresRoles = (rolesIds) => {
  if (!rolesIds || rolesIds.length === 0) return 'Sin roles asignados'
  return rolesDisponibles.value
    .filter(r => rolesIds.includes(r.idRol))
    .map(r => r.nombreRol)
    .join(', ') || 'Sin roles asignados'
}

// ---------- KPIs ----------
const kpis = computed(() => {
  const lista = usuarios.value
  return {
    total: lista.length,
    activos: lista.filter(u => u.estadoUsuario).length,
    inactivos: lista.filter(u => !u.estadoUsuario).length,
    rolesTotal: rolesDisponibles.value.length
  }
})

// ---------- Filtrado Reactivo ----------
const usuariosFiltrados = computed(() => {
  return usuarios.value.filter(u => {
    if (!mostrarInactivos.value && !u.estadoUsuario) return false
    if (filtroRol.value && !u.roles.includes(filtroRol.value)) return false

    if (filtroBusqueda.value.trim()) {
      const term = filtroBusqueda.value.toLowerCase().trim()
      const matchName = u.nombreUsuario.toLowerCase().includes(term)
      const matchEmail = u.correo.toLowerCase().includes(term)
      const matchRole = u.roles.some(r => r.toLowerCase().includes(term))
      return matchName || matchEmail || matchRole
    }
    return true
  })
})

// ---------- Evaluaciones Reactivas ----------
const emailEvaluation = computed(() => {
  if (!formUsuario.correo) return { isValid: false }
  return EmailRules.validate(formUsuario.correo)
})

const passwordEvaluation = computed(() => {
  if (!formUsuario.password) return { isValid: false }
  return PasswordRules.validate(formUsuario.password)
})

const passwordMismatch = computed(() => {
  if (!formUsuario.password && !confirmPassword.value) return false
  return formUsuario.password !== confirmPassword.value
})

const passwordMatch = computed(() => {
  return formUsuario.password &&
         confirmPassword.value &&
         formUsuario.password === confirmPassword.value &&
         passwordEvaluation.value.isValid
})

// ---------- Comparación de Cambios (Diff / Resumen) ----------
const resumenEdicion = computed(() => {
  if (!isEditing.value || !usuarioOriginal.value) return []

  const nomActual = formUsuario.nombreUsuario.trim()
  const nomAnt = usuarioOriginal.value.nombreUsuario.trim()

  const correoActual = formUsuario.correo.trim().toLowerCase()
  const correoAnt = usuarioOriginal.value.correo.trim().toLowerCase()

  const rolesAntOrdenados = [...usuarioOriginal.value.rolesIds].sort()
  const rolesActOrdenados = [...formUsuario.rolesIds].sort()
  const rolesCambiaron = JSON.stringify(rolesAntOrdenados) !== JSON.stringify(rolesActOrdenados)

  const passwordCambio = Boolean(formUsuario.password && formUsuario.password.trim().length > 0)

  return [
    {
      etiqueta: 'Nombre de Usuario',
      anterior: nomAnt,
      nuevo: nomActual,
      haCambiado: nomActual !== nomAnt
    },
    {
      etiqueta: 'Correo Electrónico',
      anterior: correoAnt,
      nuevo: correoActual,
      haCambiado: correoActual !== correoAnt
    },
    {
      etiqueta: 'Roles Asignados',
      anterior: obtenerNombresRoles(usuarioOriginal.value.rolesIds),
      nuevo: obtenerNombresRoles(formUsuario.rolesIds),
      haCambiado: rolesCambiaron
    },
    {
      etiqueta: 'Contraseña de Acceso',
      anterior: '•••••••• (Actual)',
      nuevo: passwordCambio ? 'Nueva clave segura asignada' : 'Sin cambios (se mantiene la actual)',
      haCambiado: passwordCambio
    }
  ]
})

const cambiosDetectados = computed(() => resumenEdicion.value.filter(c => c.haCambiado))

// ---------- Carga de Datos ----------
const cargarDatos = async () => {
  if (!can('USUARIOS', 'consultar')) {
    isLoading.value = false
    return
  }
  isLoading.value = true
  try {
    const [resUsuarios, resRoles] = await Promise.all([
      usuarioService.obtenerTodos(),
      usuarioService.obtenerRoles()
    ])
    usuarios.value = Array.isArray(resUsuarios) ? resUsuarios : (resUsuarios?.data || [])
    rolesDisponibles.value = Array.isArray(resRoles) ? resRoles : (resRoles?.data || [])
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudieron sincronizar los usuarios con el servidor.')
  } finally {
    isLoading.value = false
  }
}

// ---------- Modales Crear / Editar ----------
const limpiarErrores = () => {
  Object.keys(erroresForm).forEach(k => delete erroresForm[k])
}

const abrirModalCrear = () => {
  if (!can('USUARIOS', 'insertar')) return
  isEditing.value = false
  currentUserId.value = null
  usuarioOriginal.value = null
  confirmPassword.value = ''
  showPassword.value = false
  limpiarErrores()

  formUsuario.nombreUsuario = ''
  formUsuario.correo = ''
  formUsuario.password = ''
  formUsuario.rolesIds = []

  showModal.value = true
}

const abrirModalEditar = (user) => {
  if (!can('USUARIOS', 'modificar')) return
  isEditing.value = true
  currentUserId.value = user.idUsuario
  confirmPassword.value = ''
  showPassword.value = false
  limpiarErrores()

  usuarioOriginal.value = {
    nombreUsuario: user.nombreUsuario,
    correo: user.correo,
    rolesIds: [...user.rolesIds]
  }

  formUsuario.nombreUsuario = user.nombreUsuario
  formUsuario.correo = user.correo
  formUsuario.password = ''
  formUsuario.rolesIds = [...user.rolesIds]

  showModal.value = true
}

// ---------- Validaciones y Envío ----------
const validarFormulario = () => {
  limpiarErrores()
  const usuario = formUsuario.nombreUsuario?.trim() || ''
  const correo = formUsuario.correo?.trim() || ''

  if (!usuario) {
    erroresForm.nombreUsuario = 'El nombre de usuario es obligatorio.'
  } else if (!/^[a-zA-Z0-9_]{3,30}$/.test(usuario)) {
    erroresForm.nombreUsuario = 'Solo se admiten letras, números y guión bajo (3 a 30 caracteres).'
  }

  if (!correo) {
    erroresForm.correo = 'El correo electrónico es obligatorio.'
  } else if (!emailEvaluation.value.isValid) {
    erroresForm.correo = 'El formato del correo electrónico no es válido.'
  }

  if (!isEditing.value) {
    if (!formUsuario.password) {
      erroresForm.password = 'La contraseña es obligatoria para nuevos usuarios.'
    } else if (!passwordEvaluation.value.isValid) {
      erroresForm.password = 'La contraseña no cumple con los requisitos de seguridad.'
    }
    if (formUsuario.password !== confirmPassword.value) {
      erroresForm.confirmPassword = 'Las contraseñas no coinciden.'
    }
  }

  if (isEditing.value && formUsuario.password) {
    if (!passwordEvaluation.value.isValid) {
      erroresForm.password = 'La nueva contraseña no cumple con los requisitos de seguridad.'
    }
    if (formUsuario.password !== confirmPassword.value) {
      erroresForm.confirmPassword = 'La confirmación de la nueva contraseña no coincide.'
    }
  }

  return Object.keys(erroresForm).length === 0
}

const solicitarGuardar = () => {
  if (!validarFormulario()) return

  if (isEditing.value) {
    if (cambiosDetectados.value.length === 0) {
      mostrarToast('warning', 'No realizaste ninguna modificación en los datos del usuario.')
      return
    }
    showConfirmGuardar.value = true
  } else {
    showConfirmGuardar.value = true
  }
}

const ejecutarGuardado = async () => {
  if (isSaving.value) return
  isSaving.value = true

  const payload = {
    nombreUsuario: formUsuario.nombreUsuario.trim(),
    correo: formUsuario.correo.trim().toLowerCase(),
    password: formUsuario.password ? formUsuario.password.trim() : null,
    rolesIds: formUsuario.rolesIds
  }

  try {
    if (isEditing.value) {
      await usuarioService.actualizar(currentUserId.value, payload)
      mostrarToast('success', `Usuario "${payload.nombreUsuario}" actualizado exitosamente.`)
    } else {
      await usuarioService.crear(payload)
      mostrarToast('success', `Usuario "${payload.nombreUsuario}" registrado correctamente en el sistema.`)
    }
    showConfirmGuardar.value = false
    showModal.value = false
    await cargarDatos()
  } catch (err) {
    showConfirmGuardar.value = false
    const msg = err?.response?.data?.message || 'Error al procesar la cuenta de usuario.'
    erroresForm.general = msg
    mostrarToast('error', msg)
  } finally {
    isSaving.value = false
  }
}

// ---------- Toggle Estado y Bloqueo ----------
const solicitarToggleEstado = async (user) => {
  if (!can('USUARIOS', 'eliminar')) return

  if (user.idUsuario === 1 && user.estadoUsuario) {
    mostrarToast('warning', 'La cuenta principal de Administrador no puede ser inhabilitada.')
    return
  }

  const accion = user.estadoUsuario ? 'desactivar y bloquear' : 'activar'

  const confirmado = await pedirConfirmacion({
    variant: user.estadoUsuario ? 'danger' : 'primary',
    title: `¿Confirmar cambio de estado?`,
    message: `¿Estás seguro de que deseas ${accion} la cuenta de "${user.nombreUsuario}"?`,
    detalles: [
      `Usuario: ${user.nombreUsuario}`,
      `Correo: ${user.correo}`,
      `Roles: ${user.roles?.join(', ') || 'Sin rol'}`,
      `Estado actual: ${user.estadoUsuario ? 'Activo' : 'Inactivo / Bloqueado'}`
    ],
    confirmText: `Sí, ${accion}`,
    cancelText: 'Cancelar'
  })

  if (!confirmado) return

  try {
    const res = await usuarioService.cambiarEstado(user.idUsuario)
    mostrarToast('success', res?.message || `Cuenta ${accion}da exitosamente.`)
    await cargarDatos()
  } catch (err) {
    mostrarToast('error', err?.response?.data?.message || 'No se pudo alternar el estado del usuario.')
  }
}

// Control Teclado (Escape)
const onKeydown = (e) => {
  if (e.key !== 'Escape') return
  if (confirmDialog.visible) resolverConfirmacion(false)
  else if (showConfirmGuardar.value) showConfirmGuardar.value = false
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
    <!-- Modal Confirmación de Cambios / Resumen (Diff Visual) -->
    <Transition name="fade">
      <div v-if="showConfirmGuardar" class="fixed inset-0 z-[65] flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4" role="dialog" aria-modal="true">
        <div class="bg-white rounded-3xl p-6 sm:p-7 shadow-2xl w-full max-w-md border border-slate-100 text-center animate-scale-in max-h-[90vh] flex flex-col">
          <div class="w-16 h-16 rounded-full bg-purple-50 text-purple-600 flex items-center justify-center mx-auto mb-4 border border-purple-100 shadow-sm shrink-0">
            <svg class="w-8 h-8 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
          </div>

          <h3 class="text-lg font-black text-slate-800 tracking-tight shrink-0">
            {{ isEditing ? '¿Guardar los cambios en el usuario?' : '¿Confirmar registro de usuario?' }}
          </h3>
          <p class="text-xs text-slate-500 font-medium mt-1 mb-4 shrink-0">
            {{ isEditing ? 'Compara la información antes de aplicar la modificación.' : 'Revisa los datos y roles que se asignarán a esta nueva cuenta.' }}
          </p>

          <!-- Si es Edición: Diff Visual -->
          <div v-if="isEditing" class="flex-1 overflow-y-auto text-left space-y-3 mb-5 bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
            <div
              v-for="item in resumenEdicion"
              :key="item.etiqueta"
              class="text-xs pb-2.5 last:pb-0 border-b border-slate-200/70 last:border-0"
            >
              <div class="flex items-center justify-between mb-1">
                <span class="font-bold text-slate-500 uppercase text-[10px] tracking-wider">{{ item.etiqueta }}</span>
                <span
                  v-if="item.haCambiado"
                  class="text-[10px] font-bold text-purple-700 bg-purple-100 px-2 py-0.5 rounded-full border border-purple-200"
                >
                  Modificado
                </span>
                <span v-else class="text-[10px] font-semibold text-slate-400">Sin cambio</span>
              </div>

              <div v-if="item.haCambiado" class="space-y-0.5">
                <div class="text-[11px] text-slate-400 line-through">
                  <span class="font-semibold text-slate-400">Anterior:</span> {{ item.anterior }}
                </div>
                <div class="text-xs font-bold text-slate-800">
                  <span class="text-purple-600 font-bold">Nuevo:</span> {{ item.nuevo }}
                </div>
              </div>

              <div v-else class="text-xs font-semibold text-slate-700">
                {{ item.nuevo }}
              </div>
            </div>
          </div>

          <!-- Si es Inserción: Resumen Estructurado (Sin exponer la clave) -->
          <div v-else class="flex-1 overflow-y-auto text-left space-y-2.5 mb-5 bg-slate-50/80 p-4 rounded-2xl border border-slate-200/80">
            <div class="flex justify-between items-center text-xs pb-2 border-b border-slate-200/60">
              <span class="font-bold text-slate-500 uppercase text-[10px]">Nombre de Usuario:</span>
              <span class="font-bold text-slate-800">{{ formUsuario.nombreUsuario.trim() }}</span>
            </div>
            <div class="flex justify-between items-center text-xs pb-2 border-b border-slate-200/60">
              <span class="font-bold text-slate-500 uppercase text-[10px]">Correo Electrónico:</span>
              <span class="font-bold text-slate-800">{{ formUsuario.correo.trim().toLowerCase() }}</span>
            </div>
            <div class="flex justify-between items-center text-xs pb-2 border-b border-slate-200/60">
              <span class="font-bold text-slate-500 uppercase text-[10px]">Roles Asignados:</span>
              <span class="font-bold text-purple-700">{{ obtenerNombresRoles(formUsuario.rolesIds) }}</span>
            </div>
            <div class="flex justify-between items-center text-xs pt-0.5">
              <span class="font-bold text-slate-500 uppercase text-[10px]">Seguridad de Acceso:</span>
              <span class="text-emerald-700 font-bold flex items-center gap-1">
                <span>✓ Contraseña robusta validada</span>
              </span>
            </div>
          </div>

          <div class="flex items-center gap-3 shrink-0 pt-2 border-t border-slate-100">
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
              {{ isSaving ? 'Guardando...' : (isEditing ? 'Sí, confirmar cambios' : 'Sí, registrar usuario') }}
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
            <p class="text-xs text-slate-500 font-medium mt-1.5 leading-relaxed">{{ confirmDialog.message }}</p>

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
    <div v-if="!can('USUARIOS', 'consultar')" class="bg-white rounded-3xl p-14 text-center shadow-md border border-slate-100">
      <div class="w-16 h-16 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4 ring-8 ring-rose-50/60">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>
      </div>
      <h3 class="text-base font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-sm text-slate-500 mt-1.5 max-w-sm mx-auto">No cuentas con autorización para consultar la administración de cuentas y usuarios.</p>
    </div>

    <!-- Contenedor Principal -->
    <div v-else class="space-y-6">
      <!-- Encabezado con Botón Principal -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Administración de Cuentas y Accesos</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control granular de credenciales, roles, intentos de acceso y bloqueos temporales</p>
        </div>
        <button 
          v-if="can('USUARIOS', 'insertar')"
          @click="abrirModalCrear"
          class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-gradient-to-tl from-purple-700 to-pink-500 text-white text-xs font-bold rounded-xl shadow-md shadow-purple-500/30 hover:shadow-lg hover:shadow-purple-500/40 hover:scale-[1.01] active:scale-[0.98] transition-all whitespace-nowrap"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
          </svg>
          Nuevo Usuario
        </button>
      </div>

      <!-- Tarjetas KPIs Informativas -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Total Usuarios</p>
            <p class="text-2xl font-black text-slate-900 mt-1 font-mono">{{ kpis.total }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-purple-50 text-purple-700 flex items-center justify-center border border-purple-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Cuentas Activas</p>
            <p class="text-2xl font-black text-emerald-600 mt-1 font-mono">{{ kpis.activos }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center border border-emerald-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Inactivas / Bloqueadas</p>
            <p class="text-2xl font-black text-rose-600 mt-1 font-mono">{{ kpis.inactivos }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-rose-50 text-rose-600 flex items-center justify-center border border-rose-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-md border border-slate-100 p-4 flex items-center justify-between">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Roles en Catálogo</p>
            <p class="text-2xl font-black text-indigo-600 mt-1 font-mono">{{ kpis.rolesTotal }}</p>
          </div>
          <div class="w-11 h-11 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center border border-indigo-100 shadow-sm">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" /></svg>
          </div>
        </div>
      </div>

      <!-- Barra de Filtros y Búsqueda -->
      <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 flex flex-col md:flex-row md:items-center gap-3">
        <div class="relative flex-1">
          <input 
            v-model="filtroBusqueda" 
            type="text" 
            placeholder="Buscar por usuario, correo electrónico o rol asignado..." 
            class="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-all"
          />
          <svg class="w-4 h-4 text-slate-400 absolute left-3.5 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <select 
            v-model="filtroRol" 
            class="px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-bold text-slate-700 outline-none focus:bg-white focus:border-purple-500"
          >
            <option value="">Todos los roles</option>
            <option v-for="rol in rolesDisponibles" :key="rol.idRol" :value="rol.nombreRol">
              {{ rol.nombreRol }}
            </option>
          </select>

          <label class="flex items-center gap-2 text-xs font-bold text-slate-600 bg-slate-50 px-3.5 py-2.5 rounded-xl border border-slate-200 cursor-pointer select-none">
            <input 
              type="checkbox" 
              v-model="mostrarInactivos" 
              class="rounded text-purple-600 focus:ring-purple-500"
            />
            <span>Mostrar inactivos / bloqueados</span>
          </label>
        </div>
      </div>

      <!-- Tabla de Datos -->
      <div class="bg-white rounded-2xl shadow-md border border-slate-100 overflow-hidden">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-100">
          <h3 class="text-sm font-black text-slate-800">Cuentas Registradas en el Sistema</h3>
          <span v-if="!isLoading" class="text-xs font-semibold text-slate-500">
            {{ usuariosFiltrados.length }} {{ usuariosFiltrados.length === 1 ? 'cuenta' : 'cuentas' }}
          </span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[950px] text-left border-collapse">
            <thead>
              <tr class="bg-slate-50/70 border-b border-slate-100 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                <th class="py-3.5 px-6 whitespace-nowrap min-w-[200px]">Usuario</th>
                <th class="py-3.5 px-4 whitespace-nowrap min-w-[190px]">Correo Electrónico</th>
                <th class="py-3.5 px-4 min-w-[180px]">Roles Asignados</th>
                <th class="py-3.5 px-4 whitespace-nowrap min-w-[160px]">Último Acceso</th>
                <th class="py-3.5 px-4 text-center whitespace-nowrap min-w-[120px]">Estado</th>
                <th class="py-3.5 px-6 text-right whitespace-nowrap min-w-[150px]">Acciones</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-xs">
              <tr v-if="isLoading">
                <td colspan="6" class="py-14 text-center text-slate-400 font-semibold">
                  Cargando cuentas de usuario...
                </td>
              </tr>

              <tr 
                v-for="user in usuariosFiltrados" 
                :key="user.idUsuario" 
                :class="['hover:bg-purple-50/20 transition-colors', !user.estadoUsuario ? 'opacity-60 bg-slate-50/40' : '']"
              >
                <!-- Usuario con Avatar -->
                <td class="py-3.5 px-6 whitespace-nowrap">
                  <div class="flex items-center gap-3">
                    <div class="w-9 h-9 rounded-xl bg-gradient-to-tl from-purple-700 to-pink-500 text-white flex items-center justify-center font-bold text-xs uppercase shadow-sm shadow-purple-500/20 ring-2 ring-white">
                      {{ user.nombreUsuario.slice(0, 2) }}
                    </div>
                    <div>
                      <span class="block text-slate-900 font-bold text-[13px] leading-tight">
                        {{ user.nombreUsuario }}
                      </span>
                      <span class="text-[10px] font-mono text-slate-400">ID: #{{ user.idUsuario }}</span>
                    </div>
                  </div>
                </td>

                <!-- Correo -->
                <td class="py-3.5 px-4 whitespace-nowrap text-slate-700 font-medium">
                  {{ user.correo }}
                </td>

                <!-- Roles -->
                <td class="py-3.5 px-4">
                  <div class="flex flex-wrap gap-1.5">
                    <span 
                      v-for="rol in user.roles" 
                      :key="rol" 
                      class="px-2.5 py-0.5 rounded-md text-[10px] font-bold bg-purple-50 text-purple-700 border border-purple-200/80 shadow-2xs whitespace-nowrap"
                    >
                      {{ rol }}
                    </span>
                    <span v-if="user.roles.length === 0" class="text-slate-400 text-[11px] italic">Sin rol asignado</span>
                  </div>
                </td>

                <!-- Último Acceso -->
                <td class="py-3.5 px-4 whitespace-nowrap text-slate-600 font-medium font-mono text-[11px]">
                  {{ formatFecha(user.fechaUltimoAcceso) }}
                </td>

                <!-- Estado -->
                <td class="py-3.5 px-4 text-center whitespace-nowrap">
                  <div>
                    <span 
                      :class="[
                        'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-black uppercase tracking-wider border shadow-2xs',
                        user.estadoUsuario ? 'bg-emerald-50 text-emerald-700 border-emerald-300' : 'bg-rose-50 text-rose-700 border-rose-300'
                      ]"
                    >
                      <span :class="['w-1.5 h-1.5 rounded-full', user.estadoUsuario ? 'bg-emerald-500' : 'bg-rose-500']"></span>
                      {{ user.estadoUsuario ? 'Activo' : 'Inactivo' }}
                    </span>
                    <!-- Indicador de Bloqueo Temporal -->
                    <p v-if="!user.estadoUsuario && user.fechaBloqueo" class="text-[10px] text-rose-500 font-bold mt-1">
                      🔒 Bloqueado
                    </p>
                  </div>
                </td>

                <!-- Acciones -->
                <td class="py-3.5 px-6 text-right whitespace-nowrap">
                  <div class="flex items-center justify-end gap-1.5">
                    <button 
                      v-if="can('USUARIOS', 'modificar')"
                      @click="abrirModalEditar(user)"
                      class="px-2.5 py-1.5 bg-slate-100 hover:bg-purple-600 text-slate-700 hover:text-white rounded-lg text-xs font-bold transition-all shadow-xs border border-slate-200"
                    >
                      Editar
                    </button>
                    <button 
                      v-if="can('USUARIOS', 'eliminar')"
                      @click="solicitarToggleEstado(user)"
                      :class="[
                        'px-2.5 py-1.5 rounded-lg text-xs font-bold transition-all border shadow-xs',
                        user.estadoUsuario 
                          ? 'bg-rose-50 hover:bg-rose-600 text-rose-700 hover:text-white border-rose-200' 
                          : 'bg-emerald-50 hover:bg-emerald-600 text-emerald-700 hover:text-white border-emerald-200'
                      ]"
                    >
                      {{ user.estadoUsuario ? 'Desactivar' : 'Activar' }}
                    </button>
                  </div>
                </td>
              </tr>

              <tr v-if="usuariosFiltrados.length === 0 && !isLoading">
                <td colspan="6" class="py-14 text-center">
                  <div class="w-14 h-14 bg-purple-50 text-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-3 border border-purple-100">
                    <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" /></svg>
                  </div>
                  <p class="text-sm font-black text-slate-800">No se encontraron cuentas que coincidan con la búsqueda</p>
                  <p class="text-xs text-slate-500 font-medium mt-1">Ajusta los términos de búsqueda o registra un nuevo usuario.</p>
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
        <div class="modal-card bg-white rounded-3xl shadow-2xl w-full max-w-md border border-slate-100 max-h-[92vh] flex flex-col overflow-hidden animate-scale-in">
          <div class="h-1.5 bg-gradient-to-r from-purple-700 to-pink-500"></div>

          <!-- Cabecera -->
          <div class="px-7 py-4.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="font-black text-slate-900 text-base">
                {{ isEditing ? 'Editar Cuenta de Usuario' : 'Registrar Nuevo Usuario' }}
              </h3>
              <p class="text-xs text-slate-500 font-semibold mt-0.5">Configura credenciales y roles de acceso al sistema</p>
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

              <!-- Nombre de Usuario -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Nombre de Usuario <span class="text-rose-500">*</span>
                </label>
                <input 
                  v-model="formUsuario.nombreUsuario" 
                  @keypress="allowOnly.usuarioInput($event)"
                  required 
                  maxlength="30"
                  type="text" 
                  placeholder="Ej: usu_maquila"
                  class="w-full px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition" 
                />
                <p v-if="erroresForm.nombreUsuario" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.nombreUsuario }}
                </p>
              </div>

              <!-- Correo Electrónico -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Correo Electrónico <span class="text-rose-500">*</span>
                </label>
                <input 
                  v-model="formUsuario.correo" 
                  required 
                  type="email" 
                  placeholder="operador@maquila.com"
                  :class="[
                    'w-full px-3.5 py-2.5 bg-slate-50 border rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 transition',
                    formUsuario.correo && !emailEvaluation.isValid ? 'border-rose-400 focus:border-rose-500' : 'border-slate-200 focus:border-purple-500'
                  ]"
                />
                <div v-if="formUsuario.correo && !emailEvaluation.isValid" class="mt-2 p-2 bg-rose-50 rounded-xl border border-rose-100 text-[11px] space-y-0.5 text-rose-700 font-semibold">
                  <div :class="emailEvaluation.hasAt ? 'text-emerald-700' : ''">• Debe incluir el símbolo '@'</div>
                  <div :class="emailEvaluation.hasDomain ? 'text-emerald-700' : ''">• Debe incluir un dominio válido (ej. .com, .gt)</div>
                  <div :class="emailEvaluation.noSpaces ? 'text-emerald-700' : ''">• No debe tener espacios</div>
                </div>
                <p v-if="erroresForm.correo" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.correo }}
                </p>
              </div>

              <!-- Contraseña -->
              <div>
                <div class="flex items-center justify-between mb-1.5">
                  <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider">
                    Contraseña <span v-if="!isEditing" class="text-rose-500">*</span>
                    <span v-else class="text-[10px] text-slate-400 font-normal lowercase">(dejar vacío para mantener la actual)</span>
                  </label>
                  <label class="flex items-center gap-1.5 text-[11px] text-slate-500 cursor-pointer select-none">
                    <input type="checkbox" v-model="showPassword" class="rounded border-slate-300 text-purple-600 h-3.5 w-3.5" />
                    <span>Mostrar</span>
                  </label>
                </div>
                <input 
                  v-model="formUsuario.password" 
                  :required="!isEditing" 
                  :type="showPassword ? 'text' : 'password'" 
                  placeholder="••••••••" 
                  :class="[
                    'w-full px-3.5 py-2.5 bg-slate-50 border rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 transition',
                    formUsuario.password && !passwordEvaluation.isValid ? 'border-rose-400 focus:border-rose-500' : 'border-slate-200 focus:border-purple-500'
                  ]" 
                />
                
                <!-- Guía Reactiva de Fortaleza de Contraseña -->
                <div v-if="formUsuario.password" class="mt-2 p-2.5 bg-slate-50 rounded-xl border border-slate-200/80 text-[11px] space-y-1">
                  <div :class="passwordEvaluation.minLength ? 'text-emerald-600 font-bold' : 'text-slate-400'">• Mínimo 8 caracteres</div>
                  <div :class="passwordEvaluation.hasUpper && passwordEvaluation.hasLower ? 'text-emerald-600 font-bold' : 'text-slate-400'">• Mayúsculas y minúsculas</div>
                  <div :class="passwordEvaluation.hasNumber ? 'text-emerald-600 font-bold' : 'text-slate-400'">• Al menos un número</div>
                  <div :class="passwordEvaluation.hasSpecial ? 'text-emerald-600 font-bold' : 'text-slate-400'">• Carácter especial (@, $, !, %, *, #, etc.)</div>
                </div>
                <p v-if="erroresForm.password" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.password }}
                </p>
              </div>

              <!-- Confirmar Contraseña -->
              <div v-if="!isEditing || formUsuario.password">
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Confirmar Contraseña <span class="text-rose-500">*</span>
                </label>
                <input 
                  v-model="confirmPassword" 
                  :required="!isEditing || Boolean(formUsuario.password)"
                  :type="showPassword ? 'text' : 'password'" 
                  placeholder="••••••••" 
                  :class="[
                    'w-full px-3.5 py-2.5 bg-slate-50 border rounded-xl text-xs font-semibold text-slate-800 placeholder-slate-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 transition',
                    passwordMismatch ? 'border-rose-400 focus:border-rose-500 bg-rose-50/20' : '',
                    passwordMatch ? 'border-emerald-400 focus:border-emerald-500 bg-emerald-50/20' : 'border-slate-200 focus:border-purple-500'
                  ]" 
                />
                <p v-if="passwordMismatch" class="text-rose-600 text-[11px] font-bold mt-1">
                  Las contraseñas ingresadas no coinciden.
                </p>
                <p v-if="erroresForm.confirmPassword" class="text-rose-600 text-[11px] font-bold mt-1">
                  {{ erroresForm.confirmPassword }}
                </p>
              </div>

              <!-- Asignación de Roles -->
              <div>
                <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
                  Asignación de Roles
                </label>
                <div class="grid grid-cols-1 gap-1.5 max-h-36 overflow-y-auto p-2 bg-slate-50 border border-slate-200 rounded-xl">
                  <label 
                    v-for="rol in rolesDisponibles" 
                    :key="rol.idRol" 
                    class="flex items-center space-x-2 text-xs text-slate-700 cursor-pointer hover:bg-white p-2 rounded-lg transition"
                  >
                    <input 
                      type="checkbox" 
                      :value="rol.idRol" 
                      v-model="formUsuario.rolesIds" 
                      class="rounded border-slate-300 text-purple-600 focus:ring-purple-500 h-4 w-4"
                    />
                    <div>
                      <span class="font-bold text-slate-800">{{ rol.nombreRol }}</span>
                      <p v-if="rol.descripcion" class="text-[10px] text-slate-400 truncate">{{ rol.descripcion }}</p>
                    </div>
                  </label>
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
                {{ isSaving ? 'Guardando...' : (isEditing ? 'Revisar y Guardar' : 'Revisar y Registrar') }}
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