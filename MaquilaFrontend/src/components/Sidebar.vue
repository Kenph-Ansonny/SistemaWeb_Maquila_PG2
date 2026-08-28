<script setup>
import { computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'

const props = defineProps({
  userRole: {
    type: String,
    default: 'admin' // 'admin', 'inventario', 'pedidos'
  }
})

const router = useRouter()
const route = useRoute()

// Definición de módulos con los roles permitidos para cada uno
const allMenuItems = [
  { id: 'dashboard', name: 'Dashboard', path: '/dashboard', roles: ['admin', 'inventario', 'pedidos'], icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6' },
  { id: 'inventario', name: 'Inventario / Telas', path: '/inventario', roles: ['admin', 'inventario'], icon: 'M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4' },
  { id: 'compras', name: 'Módulo Compras', path: '/compras', roles: ['admin'], icon: 'M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z' },
  { id: 'ventas', name: 'Módulo Ventas', path: '/ventas', roles: ['admin'], icon: 'M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z' },
  { id: 'pedidos', name: 'Pedidos Maquila', path: '/pedidos', roles: ['admin', 'pedidos'], icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01' },
]

// Menús filtrados por rol
const visibleMenuItems = computed(() => {
  return allMenuItems.filter(item => item.roles.includes(props.userRole))
})

const handleLogout = () => {
  localStorage.removeItem('user_role')
  router.push('/')
}
</script>

<template>
  <aside class="fixed inset-y-0 left-0 z-50 w-64 my-4 ml-4 bg-white shadow-xl rounded-2xl flex flex-col transition-all duration-300">
    <!-- Logo -->
    <div class="h-20 flex items-center px-6 border-b border-slate-100">
      <div class="h-9 w-9 rounded-xl bg-gradient-to-tl from-purple-700 to-pink-500 flex items-center justify-center text-white shadow-md">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
        </svg>
      </div>
      <span class="ml-3 font-bold text-slate-800 tracking-tight text-sm uppercase">Soft UI Maquila</span>
    </div>

    <!-- Navegación -->
    <div class="flex-1 overflow-y-auto px-4 py-4 space-y-1">
      <p class="text-xs font-bold text-slate-400 px-3 uppercase tracking-wider mb-2">Módulos</p>
      
      <button
        v-for="item in visibleMenuItems"
        :key="item.id"
        @click="router.push(item.path)"
        :class="[
          'w-full flex items-center px-4 py-3 rounded-xl font-medium text-sm transition-all duration-200',
          route.path === item.path
            ? 'bg-white shadow-lg text-slate-800 font-semibold' 
            : 'text-slate-500 hover:bg-slate-50 hover:text-slate-700'
        ]"
      >
        <div :class="[
          'w-8 h-8 rounded-lg flex items-center justify-center mr-3 transition-colors shadow-sm',
          route.path === item.path ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white' : 'bg-slate-100 text-slate-600'
        ]">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="item.icon" />
          </svg>
        </div>
        {{ item.name }}
      </button>
    </div>

    <!-- Salir -->
    <div class="p-4 border-t border-slate-100">
      <button @click="handleLogout" class="w-full flex items-center px-4 py-2 text-sm text-red-500 hover:bg-red-50 rounded-xl transition">
        <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
        </svg>
        Cerrar Sesión
      </button>
    </div>
  </aside>
</template>