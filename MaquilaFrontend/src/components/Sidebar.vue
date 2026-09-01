<script setup>
import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'

const props = defineProps({
  userRole: {
    type: String,
    default: 'admin'
  },
  isOpen: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['close'])

const router = useRouter()
const route = useRoute()

// Estado reactivo para controlar qué acordeón de módulo está expandido
const expandedModule = ref(null)

// Definición exhaustiva de módulos y submódulos basada en la base de datos
const allModules = [
  {
    id: 'dashboard',
    name: 'Dashboard',
    icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
    path: '/dashboard',
    roles: ['admin', 'inventario', 'pedidos', 'contador'],
    subItems: [] // Sin submenú, navegación directa
  },
  {
    id: 'inventario',
    name: 'Inventario & Kardex',
    icon: 'M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4',
    roles: ['admin', 'inventario'],
    subItems: [
      { id: 'articulos', name: 'Artículos y Telas', path: '/inventario/articulos' },
      { id: 'almacenes', name: 'Almacenes / Bodegas', path: '/inventario/almacenes' },
      { id: 'unidades', name: 'Unidades y Conversiones', path: '/inventario/unidades' },
      { id: 'kardex', name: 'Kardex Transaccional', path: '/inventario/kardex' }
    ]
  },
  {
    id: 'produccion',
    name: 'Producción & Recetas',
    icon: 'M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z',
    roles: ['admin', 'inventario'],
    subItems: [
      { id: 'recetas', name: 'Fichas Técnicas (BOM)', path: '/produccion/recetas' },
      { id: 'ordenes', name: 'Órdenes de Producción', path: '/produccion/ordenes' },
      { id: 'mermas', name: 'Control de Mermas', path: '/produccion/mermas' }
    ]
  },
  {
    id: 'compras',
    name: 'Módulo Compras',
    icon: 'M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z',
    roles: ['admin', 'contador'],
    subItems: [
      { id: 'proveedores', name: 'Proveedores', path: '/compras/proveedores' },
      { id: 'registro-compras', name: 'Registro de Compras', path: '/compras/registro' }
    ]
  },
  {
    id: 'pedidos',
    name: 'Pedidos & Clientes',
    icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01',
    roles: ['admin', 'pedidos'],
    subItems: [
      { id: 'clientes', name: 'Catálogo Clientes', path: '/pedidos/clientes' },
      { id: 'lista-pedidos', name: 'Pedidos de Maquila', path: '/pedidos/lista' }
    ]
  },
  {
    id: 'seguridad',
    name: 'Seguridad & Accesos',
    icon: 'M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z',
    roles: ['admin'],
    subItems: [
      { id: 'usuarios', name: 'Gestión Usuarios', path: '/seguridad/usuarios' },
      { id: 'roles-permisos', name: 'Roles y Permisos', path: '/seguridad/roles' },
      { id: 'bitacora', name: 'Bitácora de Auditoría', path: '/seguridad/bitacora' }
    ]
  }
]

// Filtro de módulos permitidos según el rol del usuario
const visibleModules = computed(() => {
  return allModules.filter(module => module.roles.includes(props.userRole))
})

// Abre el acordeón correspondiente de forma automática según la URL activa
watch(
  () => route.path,
  (currentPath) => {
    const parent = allModules.find(m => 
      m.path === currentPath || m.subItems.some(sub => sub.path === currentPath)
    )
    if (parent && parent.subItems.length > 0) {
      expandedModule.value = parent.id
    }
  },
  { immediate: true }
)

// Alterna la apertura/cierre del acordeón
const toggleModule = (module) => {
  if (module.subItems.length === 0) {
    navigateTo(module.path)
    return
  }
  expandedModule.value = expandedModule.value === module.id ? null : module.id
}

const navigateTo = (path) => {
  router.push(path)
  emit('close')
}

const isModuleActive = (module) => {
  if (module.path && route.path === module.path) return true
  return module.subItems.some(sub => sub.path === route.path)
}

const handleLogout = () => {
  localStorage.removeItem('user_role')
  localStorage.removeItem('user_name')
  router.push('/')
}
</script>

<template>
  <div>
    <!-- Backdrop oscuro en móvil/tablets -->
    <div 
      v-if="isOpen" 
      @click="emit('close')"
      class="fixed inset-0 z-40 bg-slate-900/50 backdrop-blur-sm xl:hidden transition-opacity duration-300"
    ></div>

    <!-- Contenedor del Sidebar -->
    <aside 
      :class="[
        'fixed inset-y-0 left-0 z-50 w-64 my-4 ml-4 bg-white shadow-xl rounded-2xl flex flex-col transition-transform duration-300 ease-in-out',
        isOpen ? 'translate-x-0' : '-translate-x-full xl:translate-x-0'
      ]"
    >
      <!-- Encabezado / Logo -->
      <div class="h-20 flex items-center justify-between px-6 border-b border-slate-100 flex-shrink-0">
        <div class="flex items-center">
          <div class="h-9 w-9 rounded-xl bg-gradient-to-tl from-purple-700 to-pink-500 flex items-center justify-center text-white shadow-md">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          </div>
          <span class="ml-3 font-bold text-slate-800 tracking-tight text-sm uppercase">Sistema Interno</span>
        </div>

        <button @click="emit('close')" class="xl:hidden text-slate-400 hover:text-slate-600 p-1">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Menús y Acordeones -->
      <div class="flex-1 overflow-y-auto px-3 py-4 space-y-1.5 scrollbar-thin">
        <p class="text-[11px] font-bold text-slate-400 px-3 uppercase tracking-wider mb-2">Menú Principal</p>
        
        <div v-for="module in visibleModules" :key="module.id" class="space-y-1">
          <!-- Botón de Módulo Padre -->
          <button
            @click="toggleModule(module)"
            :class="[
              'w-full flex items-center justify-between px-3 py-2.5 rounded-xl font-medium text-xs transition-all duration-200',
              isModuleActive(module)
                ? 'bg-white shadow-md text-slate-800 font-semibold' 
                : 'text-slate-500 hover:bg-slate-50 hover:text-slate-700'
            ]"
          >
            <div class="flex items-center">
              <div :class="[
                'w-7 h-7 rounded-lg flex items-center justify-center mr-2.5 transition-colors shadow-sm',
                isModuleActive(module) ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white' : 'bg-slate-100 text-slate-600'
              ]">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="module.icon" />
                </svg>
              </div>
              <span class="truncate">{{ module.name }}</span>
            </div>

            <!-- Flecha de Acordeón -->
            <svg 
              v-if="module.subItems.length > 0"
              :class="[
                'w-3.5 h-3.5 text-slate-400 transition-transform duration-200',
                expandedModule === module.id ? 'rotate-180 text-purple-600' : ''
              ]"
              fill="none" 
              stroke="currentColor" 
              viewBox="0 0 24 24"
            >
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
            </svg>
          </button>

          <!-- Listado Desplegable de Submenús -->
          <div 
            v-if="module.subItems.length > 0 && expandedModule === module.id"
            class="pl-9 pr-2 py-1 space-y-1 transition-all duration-200"
          >
            <button
              v-for="sub in module.subItems"
              :key="sub.id"
              @click="navigateTo(sub.path)"
              :class="[
                'w-full text-left px-3 py-1.5 rounded-lg text-xs font-medium transition-colors flex items-center',
                route.path === sub.path 
                  ? 'bg-purple-50 text-purple-700 font-bold shadow-xs' 
                  : 'text-slate-500 hover:text-slate-800 hover:bg-slate-50'
              ]"
            >
              <span 
                :class="[
                  'w-1.5 h-1.5 rounded-full mr-2', 
                  route.path === sub.path ? 'bg-purple-600' : 'bg-slate-300'
                ]"
              ></span>
              <span class="truncate">{{ sub.name }}</span>
            </button>
          </div>
        </div>
      </div>

      <!-- Footer / Botón Cerrar Sesión -->
      <div class="p-3 border-t border-slate-100 flex-shrink-0">
        <button @click="handleLogout" class="w-full flex items-center px-3 py-2 text-xs font-semibold text-red-500 hover:bg-red-50 rounded-xl transition">
          <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
          </svg>
          Cerrar Sesión
        </button>
      </div>
    </aside>
  </div>
</template>