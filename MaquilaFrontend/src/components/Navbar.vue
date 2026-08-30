<script setup>
defineProps({
  currentRoute: {
    type: String,
    default: 'Dashboard'
  },
  userName: {
    type: String,
    default: 'Usuario'
  }
})

const emit = defineEmits(['toggle-sidebar'])
</script>

<template>
  <header class="sticky top-4 z-30 bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-4 sm:px-6 py-3 mb-6 flex items-center justify-between border border-white/40 gap-3">
    <!-- Botón Hamburguesa + Breadcrumbs -->
    <div class="flex items-center space-x-3">
      <!-- Botón de alternar menú para pantallas móviles/tablets -->
      <button 
        @click="emit('toggle-sidebar')"
        class="xl:hidden p-2 rounded-xl text-slate-600 hover:bg-slate-100 transition focus:outline-none"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
        </svg>
      </button>

      <div>
        <nav class="text-[10px] sm:text-xs text-slate-400 font-medium">
          <span>Pages</span> / <span class="text-slate-700 font-semibold">{{ currentRoute }}</span>
        </nav>
        <h1 class="text-sm sm:text-base font-bold text-slate-800 capitalize leading-tight truncate max-w-[140px] sm:max-w-none">
          {{ currentRoute }}
        </h1>
      </div>
    </div>

    <!-- Buscador y Usuario -->
    <div class="flex items-center space-x-2 sm:space-x-4">
      <!-- Buscador responsivo -->
      <div class="relative">
        <input
          type="text"
          placeholder="Buscar..."
          class="w-32 sm:w-48 md:w-64 pl-8 sm:pl-9 pr-3 py-1.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition shadow-inner"
        />
        <svg class="w-3.5 h-3.5 sm:w-4 sm:h-4 text-slate-400 absolute left-2.5 sm:left-3 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </div>

      <!-- Avatar y Nombre -->
      <div class="flex items-center space-x-2 border-l border-slate-200 pl-2 sm:pl-4">
        <div class="w-7 h-7 sm:w-8 sm:h-8 rounded-full bg-slate-200 flex items-center justify-center font-bold text-slate-600 text-xs shadow-inner uppercase">
          {{ userName.slice(0, 2) }}
        </div>
        <span class="text-xs font-semibold text-slate-700 hidden md:inline">{{ userName }}</span>
      </div>
    </div>
  </header>
</template>