<script setup>
import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { usePermissions } from '../composables/usePermissions'

const props = defineProps({
  isOpen: {
    type: Boolean,
    default: false
  },
  isCollapsed: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['close', 'update:isCollapsed'])

const router = useRouter()
const route = useRoute()
const { can } = usePermissions()

const expandedModule = ref(null)

// Alterna el estado colapsado y notifica al padre (MainLayout)
const toggleCollapse = () => {
  const newState = !props.isCollapsed
  emit('update:isCollapsed', newState)
  if (newState) {
    expandedModule.value = null
  }
}

// Catálogo de navegación mapeado con los códigos de la tabla 'Modulos'
const allModules = [
  {
    id: 'dashboard',
    name: 'Dashboard',
    icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
    path: '/dashboard',
    moduleCode: null,
    subItems: []
  },
  {
    id: 'inventario',
    name: 'Inventario & Kardex',
    icon: 'M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4',
    subItems: [
      { id: 'articulos', name: 'Artículos y Telas', path: '/inventario/articulos', moduleCode: 'ARTICULOS' },
      { id: 'almacenes', name: 'Almacenes / Bodegas', path: '/inventario/almacenes', moduleCode: 'ALMACENES' },
      { id: 'unidades', name: 'Unidades y Conversiones', path: '/inventario/unidades', moduleCode: 'UNIDADES' },
      { id: 'kardex', name: 'Kardex Transaccional', path: '/inventario/kardex', moduleCode: 'KARDEX' }
    ]
  },
  {
    id: 'produccion',
    name: 'Producción & Recetas',
    icon: 'M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z',
    subItems: [
      { id: 'recetas', name: 'Fichas Técnicas (BOM)', path: '/produccion/recetas', moduleCode: 'RECETAS' },
      { id: 'ordenes', name: 'Órdenes de Producción', path: '/produccion/ordenes', moduleCode: 'PRODUCCION' },
      { id: 'mermas', name: 'Control de Mermas', path: '/produccion/mermas', moduleCode: 'MERMAS' }
    ]
  },
  {
    id: 'compras',
    name: 'Módulo Compras',
    icon: 'M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z',
    subItems: [
      { id: 'proveedores', name: 'Proveedores', path: '/compras/proveedores', moduleCode: 'PROVEEDORES' },
      { id: 'registro-compras', name: 'Registro de Compras', path: '/compras/registro', moduleCode: 'COMPRAS' }
    ]
  },
  {
    id: 'pedidos',
    name: 'Pedidos & Clientes',
    icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01',
    subItems: [
      { id: 'clientes', name: 'Catálogo Clientes', path: '/pedidos/clientes', moduleCode: 'CLIENTES' },
      { id: 'lista-pedidos', name: 'Pedidos de Maquila', path: '/pedidos/lista', moduleCode: 'PEDIDOS' }
    ]
  },
  {
    id: 'seguridad',
    name: 'Seguridad & Accesos',
    icon: 'M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z',
    subItems: [
      { id: 'usuarios', name: 'Gestión Usuarios', path: '/seguridad/usuarios', moduleCode: 'USUARIOS' },
      { id: 'roles-permisos', name: 'Roles y Permisos', path: '/seguridad/roles', moduleCode: 'ROLES' },
      { id: 'bitacora', name: 'Bitácora de Auditoría', path: '/seguridad/bitacora', moduleCode: 'USUARIOS' }
    ]
  }
]

const visibleModules = computed(() => {
  return allModules
    .map(module => {
      if (module.subItems.length === 0) return module
      const allowedSubItems = module.subItems.filter(sub => can(sub.moduleCode, 'consultar'))
      return { ...module, subItems: allowedSubItems }
    })
    .filter(module => module.subItems.length > 0 || module.path === '/dashboard')
})

watch(
  () => route.path,
  (currentPath) => {
    if (props.isCollapsed) return
    const parent = visibleModules.value.find(m => 
      m.path === currentPath || m.subItems.some(sub => sub.path === currentPath)
    )
    if (parent && parent.subItems.length > 0) {
      expandedModule.value = parent.id
    }
  },
  { immediate: true }
)

const toggleModule = (module) => {
  if (props.isCollapsed) {
    emit('update:isCollapsed', false)
    if (module.subItems.length > 0) {
      expandedModule.value = module.id
    } else {
      navigateTo(module.path)
    }
    return
  }

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
  localStorage.clear()
  router.push('/')
}
</script>

<template>
  <div>
    <!-- Backdrop móvil -->
    <div 
      v-if="isOpen" 
      @click="emit('close')"
      class="fixed inset-0 z-40 bg-slate-900/60 backdrop-blur-sm xl:hidden transition-opacity duration-300"
    ></div>

    <!-- Contenedor del Sidebar -->
    <aside 
      :class="[
        'fixed inset-y-0 left-0 z-50 my-4 ml-4 bg-white shadow-2xl rounded-2xl flex flex-col border border-slate-100 transition-all duration-300 ease-in-out',
        isOpen ? 'translate-x-0' : '-translate-x-full xl:translate-x-0',
        isCollapsed ? 'xl:w-20 w-64' : 'w-64'
      ]"
    >
      <!-- Encabezado / Logo -->
      <div 
        class="h-20 flex items-center border-b border-slate-100 flex-shrink-0 px-3"
        :class="isCollapsed ? 'justify-center' : 'justify-between px-5'"
      >
        <div class="flex items-center">
          <div class="h-10 w-10 min-w-[2.5rem] rounded-xl bg-gradient-to-tl from-purple-700 to-pink-500 flex items-center justify-center text-white shadow-md font-extrabold text-lg tracking-wider flex-shrink-0">
            M
          </div>
          
          <div v-if="!isCollapsed" class="ml-3 flex flex-col truncate">
            <span class="font-black text-slate-800 tracking-tight text-sm uppercase leading-tight">Soft UI Maquila</span>
            <span class="text-[10px] font-semibold text-purple-600 tracking-wide uppercase">Panel Textil</span>
          </div>
        </div>

        <!-- Botón para colapsar en Desktop -->
        <button 
          @click="toggleCollapse"
          class="hidden xl:flex text-slate-400 hover:text-slate-700 p-1.5 rounded-lg hover:bg-slate-100 transition-colors"
          :title="isCollapsed ? 'Expandir menú' : 'Colapsar menú'"
        >
          <svg 
            class="w-5 h-5 transition-transform duration-300" 
            :class="{ 'rotate-180': isCollapsed }"
            fill="none" 
            stroke="currentColor" 
            viewBox="0 0 24 24"
          >
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 19l-7-7 7-7m8 14l-7-7 7-7" />
          </svg>
        </button>

        <!-- Botón para cerrar en Móvil -->
        <button @click="emit('close')" class="xl:hidden text-slate-400 hover:text-slate-700 p-1.5 rounded-lg hover:bg-slate-100 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Menús y Acordeones Dinámicos -->
      <div class="flex-1 overflow-y-auto overflow-x-hidden px-3 py-4 space-y-2 scrollbar-thin">
        <p 
          class="text-[11px] font-extrabold text-slate-400 uppercase tracking-wider mb-2 text-center xl:text-left px-2"
        >
          {{ isCollapsed ? 'Menú' : 'Menú Principal' }}
        </p>
        
        <div v-for="module in visibleModules" :key="module.id" class="space-y-1 relative group">
          <!-- Botón de Módulo Principal -->
          <button
            @click="toggleModule(module)"
            :class="[
              'w-full flex items-center rounded-xl text-xs transition-all duration-200',
              isCollapsed ? 'justify-center p-2.5' : 'justify-between px-3.5 py-2.5 border-l-4',
              isModuleActive(module)
                ? (isCollapsed 
                    ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-md shadow-purple-500/30' 
                    : 'bg-slate-50/90 text-slate-900 font-bold border-purple-600 shadow-xs')
                : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900 border-transparent font-semibold'
            ]"
          >
            <div class="flex items-center">
              <div :class="[
                'w-7 h-7 rounded-lg flex items-center justify-center transition-all',
                !isCollapsed ? 'mr-2.5' : '',
                isModuleActive(module) && !isCollapsed
                  ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-purple-200 shadow-xs' 
                  : (isCollapsed && isModuleActive(module) ? 'text-white' : 'bg-slate-100 text-slate-600')
              ]">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="module.icon" />
                </svg>
              </div>

              <span v-if="!isCollapsed" class="truncate">
                {{ module.name }}
              </span>
            </div>

            <!-- Indicador Flecha -->
            <svg 
              v-if="module.subItems.length > 0 && !isCollapsed"
              :class="[
                'w-3.5 h-3.5 transition-transform duration-300',
                expandedModule === module.id ? 'rotate-180 text-purple-600 font-bold' : 'text-slate-400'
              ]"
              fill="none" 
              stroke="currentColor" 
              viewBox="0 0 24 24"
            >
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 9l-7 7-7-7" />
            </svg>
          </button>

          <!-- Floating Tooltip en Modo Colapsado -->
          <div 
            v-if="isCollapsed" 
            class="absolute left-full top-0 ml-3 hidden group-hover:flex flex-col bg-slate-900 text-white text-xs rounded-xl py-2 px-3 shadow-xl z-50 min-w-[160px] pointer-events-none group-hover:pointer-events-auto transition-all duration-200 opacity-0 group-hover:opacity-100"
          >
            <span class="font-bold border-b border-slate-700 pb-1 mb-1 text-purple-300">{{ module.name }}</span>
            <template v-if="module.subItems.length > 0">
              <button
                v-for="sub in module.subItems"
                :key="sub.id"
                @click="navigateTo(sub.path)"
                class="text-left py-1 text-slate-300 hover:text-white hover:pl-1 transition-all"
              >
                {{ sub.name }}
              </button>
            </template>
          </div>

          <!-- Animación Acordeón para Submenús -->
          <Transition name="accordion">
            <div 
              v-if="module.subItems.length > 0 && expandedModule === module.id && !isCollapsed"
              class="pl-6 pr-1 space-y-1 overflow-hidden"
            >
              <button
                v-for="sub in module.subItems"
                :key="sub.id"
                @click="navigateTo(sub.path)"
                :class="[
                  'w-full text-left px-3.5 py-2 rounded-xl text-xs transition-all duration-200 flex items-center justify-between',
                  route.path === sub.path 
                    ? 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white font-bold shadow-md shadow-purple-500/25 translate-x-1' 
                    : 'text-slate-600 font-medium hover:text-slate-900 hover:bg-slate-100/70'
                ]"
              >
                <div class="flex items-center truncate">
                  <span 
                    :class="[
                      'w-1.5 h-1.5 rounded-full mr-2.5 transition-colors', 
                      route.path === sub.path ? 'bg-white' : 'bg-slate-400'
                    ]"
                  ></span>
                  <span class="truncate">{{ sub.name }}</span>
                </div>
              </button>
            </div>
          </Transition>
        </div>
      </div>

      <!-- Footer / Cierre de Sesión -->
      <div class="p-3 border-t border-slate-100 flex-shrink-0">
        <button 
          @click="handleLogout" 
          :class="[
            'w-full flex items-center justify-center py-2.5 text-xs font-bold text-rose-600 hover:bg-rose-50 rounded-xl transition-all duration-200 border border-transparent hover:border-rose-100',
            isCollapsed ? 'px-0' : 'px-3'
          ]"
          :title="isCollapsed ? 'Cerrar Sesión' : ''"
        >
          <svg class="w-4 h-4" :class="{ 'mr-2': !isCollapsed }" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
          </svg>
          <span v-if="!isCollapsed" class="truncate">Cerrar Sesión</span>
        </button>
      </div>
    </aside>
  </div>
</template>

<style scoped>
.accordion-enter-active,
.accordion-leave-active {
  transition: max-height 0.3s cubic-bezier(0.4, 0, 0.2, 1), opacity 0.25s ease-in-out;
  max-height: 300px;
  opacity: 1;
}

.accordion-enter-from,
.accordion-leave-to {
  max-height: 0;
  opacity: 0;
}
</style>