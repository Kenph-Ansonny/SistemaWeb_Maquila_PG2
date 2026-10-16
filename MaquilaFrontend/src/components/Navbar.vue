<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { usePermissions } from '../composables/usePermissions'

defineProps({
  userName: {
    type: String,
    default: 'Usuario'
  },
  userRole: {
    type: String,
    default: 'admin'
  }
})

const emit = defineEmits(['toggle-sidebar'])
const router = useRouter()
const { can } = usePermissions()

// ---------- Estado del Buscador de Módulos ----------
const busqueda = ref('')
const searchFocused = ref(false)
const searchContainer = ref(null)

// Catálogo de rutas del sistema
const modulosSistema = [
  {
    nombre: 'Dashboard General',
    descripcion: 'Métricas clave, facturación y embudo de producción',
    path: '/dashboard',
    modulo: 'DASHBOARD',
    categoria: 'General',
    icono: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6'
  },
  {
    nombre: 'Catálogo de Artículos y Telas',
    descripcion: 'Materias primas, insumos y prendas terminadas',
    path: '/inventario/articulos',
    modulo: 'ARTICULOS',
    categoria: 'Inventario',
    icono: 'M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4'
  },
  {
    nombre: 'Kardex Transaccional',
    descripcion: 'Historial de movimientos y saldos de almacén',
    path: '/inventario/kardex',
    modulo: 'ARTICULOS',
    categoria: 'Inventario',
    icono: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2'
  },
  {
    nombre: 'Fichas Técnicas (BOM / Recetas)',
    descripcion: 'Fórmulas de confección, insumos y mermas',
    path: '/produccion/recetas',
    modulo: 'RECETAS',
    categoria: 'Producción',
    icono: 'M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z'
  },
  {
    nombre: 'Órdenes de Producción',
    descripcion: 'Programación, corte, costura y avance de taller',
    path: '/produccion/ordenes',
    modulo: 'ORDENES_PRODUCCION',
    categoria: 'Producción',
    icono: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10'
  },
  {
    nombre: 'Registro de Mermas',
    descripcion: 'Control de desperdicios y retazos en producción',
    path: '/produccion/mermas',
    modulo: 'MERMAS',
    categoria: 'Producción',
    icono: 'M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16'
  },
  {
    nombre: 'Catálogo de Proveedores',
    descripcion: 'Gestión de proveedores de telas y suministros',
    path: '/compras/proveedores',
    modulo: 'PROVEEDORES',
    categoria: 'Compras',
    icono: 'M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z'
  },
  {
    nombre: 'Registro de Compras',
    descripcion: 'Facturas de compra e ingreso formal a inventario',
    path: '/compras/registro',
    modulo: 'COMPRAS',
    categoria: 'Compras',
    icono: 'M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z'
  },
  {
    nombre: 'Gestión de Usuarios y Roles',
    descripcion: 'Cuentas de acceso, roles y matriz de permisos',
    path: '/seguridad/usuarios',
    modulo: 'USUARIOS',
    categoria: 'Seguridad',
    icono: 'M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z'
  },
  {
    nombre: 'Bitácora de Auditoría',
    descripcion: 'Registro inmutable de acciones, IP y eventos',
    path: '/seguridad/bitacora',
    modulo: 'USUARIOS',
    categoria: 'Seguridad',
    icono: 'M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z'
  }
]

// Filtra según los permisos activos del usuario
const modulosPermitidos = computed(() => {
  return modulosSistema.filter(m => !m.modulo || can(m.modulo, 'consultar'))
})

// Resultados de búsqueda
const resultadosBusqueda = computed(() => {
  const q = busqueda.value.trim().toLowerCase()
  if (!q) return []
  return modulosPermitidos.value.filter(m =>
    m.nombre.toLowerCase().includes(q) ||
    m.categoria.toLowerCase().includes(q) ||
    m.descripcion.toLowerCase().includes(q)
  )
})

const showDropdown = computed(() => {
  return searchFocused.value && busqueda.value.trim().length > 0
})

const navegarA = (path) => {
  router.push(path)
  busqueda.value = ''
  searchFocused.value = false
}

// Cierre al hacer clic fuera o presionar Escape
const handleClickOutside = (event) => {
  if (searchContainer.value && !searchContainer.value.contains(event.target)) {
    searchFocused.value = false
  }
}

const handleKeydown = (event) => {
  if (event.key === 'Escape') {
    searchFocused.value = false
  }
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
  document.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
  document.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <header class="sticky top-4 z-30 bg-white/85 backdrop-blur-md shadow-md shadow-slate-200/60 rounded-2xl px-4 sm:px-6 py-3 mb-6 flex items-center justify-between border border-white/60 gap-3">
    <!-- Lateral Izquierdo: Botón Hamburguesa móvil e indicador de estado -->
    <div class="flex items-center space-x-3 min-w-0">
      <button
        @click="emit('toggle-sidebar')"
        class="xl:hidden p-2 rounded-xl text-slate-600 hover:bg-slate-100 transition focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/25"
        aria-label="Abrir menú"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
        </svg>
      </button>

      <!-- Indicador sutil de sistema en línea sin repetir el título de la vista -->
      <div class="hidden sm:flex items-center gap-2 text-xs font-semibold text-slate-500">
        <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
        <span class="tracking-tight">Sistema Textil en Línea</span>
      </div>
    </div>

    <!-- Lateral Derecho: Buscador Interactivo de Módulos y Perfil -->
    <div class="flex items-center space-x-2 sm:space-x-4">
      <!-- Buscador de Módulos con Autocompletado -->
      <div ref="searchContainer" class="relative">
        <div class="relative flex items-center">
          <svg class="w-4 h-4 text-slate-400 absolute left-3 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            v-model="busqueda"
            @focus="searchFocused = true"
            type="text"
            placeholder="Ir a un módulo..."
            aria-label="Buscar módulo en el sistema"
            class="w-36 sm:w-56 md:w-72 h-9 pl-9 pr-8 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-700 placeholder:text-slate-400 hover:border-slate-300 focus:bg-white focus:outline-none focus:ring-4 focus:ring-purple-500/15 focus:border-purple-500 transition"
          />
          <button
            v-if="busqueda"
            @click="busqueda = ''; searchFocused = false"
            class="absolute right-2.5 text-slate-400 hover:text-slate-600 text-xs font-bold"
            title="Limpiar"
          >
            ✕
          </button>
        </div>

        <!-- Dropdown de Resultados -->
        <Transition name="dropdown">
          <div
            v-if="showDropdown"
            class="absolute top-full right-0 sm:left-0 sm:right-auto mt-2 w-80 sm:w-96 bg-white rounded-2xl shadow-2xl border border-slate-100 p-2 z-50 max-h-80 overflow-y-auto"
          >
            <div class="px-3 py-1.5 text-[10px] font-black uppercase tracking-wider text-slate-400 border-b border-slate-100 mb-1">
              Módulos coincidentes ({{ resultadosBusqueda.length }})
            </div>

            <div v-if="resultadosBusqueda.length > 0" class="space-y-1">
              <button
                v-for="item in resultadosBusqueda"
                :key="item.path"
                @click="navegarA(item.path)"
                class="w-full flex items-start gap-3 p-2.5 rounded-xl text-left hover:bg-purple-50/70 transition-colors group"
              >
                <div class="w-8 h-8 rounded-lg bg-slate-100 text-slate-600 group-hover:bg-purple-100 group-hover:text-purple-700 flex items-center justify-center shrink-0 mt-0.5 transition-colors">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="item.icono" />
                  </svg>
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center justify-between gap-2">
                    <p class="text-xs font-bold text-slate-800 group-hover:text-purple-700 truncate">
                      {{ item.nombre }}
                    </p>
                    <span class="text-[9px] font-bold px-1.5 py-0.5 rounded bg-slate-100 text-slate-500 uppercase tracking-wider shrink-0">
                      {{ item.categoria }}
                    </span>
                  </div>
                  <p class="text-[11px] text-slate-400 truncate mt-0.5">
                    {{ item.descripcion }}
                  </p>
                </div>
              </button>
            </div>

            <div v-else class="text-center py-6 px-3">
              <p class="text-xs font-bold text-slate-600">No se encontraron módulos</p>
              <p class="text-[11px] text-slate-400 mt-0.5">Intenta con otro término o revisa tus permisos de acceso.</p>
            </div>
          </div>
        </Transition>
      </div>

      <!-- Avatar y Nombre de Usuario -->
      <div class="flex items-center gap-2.5 border-l border-slate-200 pl-2 sm:pl-4">
        <div class="relative">
          <div class="w-8 h-8 sm:w-9 sm:h-9 rounded-full bg-gradient-to-tl from-purple-700 to-pink-500 flex items-center justify-center font-bold text-white text-xs shadow-md shadow-purple-500/30 uppercase ring-2 ring-white">
            {{ userName.slice(0, 2) }}
          </div>
          <span class="absolute -bottom-0.5 -right-0.5 w-2.5 h-2.5 rounded-full bg-emerald-500 ring-2 ring-white"></span>
        </div>
        <div class="hidden md:flex flex-col">
          <span class="text-xs font-bold text-slate-800 leading-tight">{{ userName }}</span>
          <span class="text-[10px] font-semibold text-purple-700 capitalize leading-tight">{{ userRole }}</span>
        </div>
      </div>
    </div>
  </header>
</template>

<style scoped>
.dropdown-enter-active,
.dropdown-leave-active {
  transition: all 0.18s cubic-bezier(0.16, 1, 0.3, 1);
}
.dropdown-enter-from,
.dropdown-leave-to {
  opacity: 0;
  transform: translateY(-8px) scale(0.98);
}
</style>