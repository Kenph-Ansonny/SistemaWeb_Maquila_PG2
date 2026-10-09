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
  <header class="sticky top-4 z-30 bg-white/85 backdrop-blur-md shadow-md shadow-slate-200/60 rounded-2xl px-4 sm:px-6 py-3 mb-6 flex items-center justify-between border border-white/60 gap-3">
    <!-- Botón Hamburguesa + Breadcrumbs -->
    <div class="flex items-center space-x-3 min-w-0">
      <!-- Botón de alternar menú para pantallas móviles/tablets -->
      <button
        @click="emit('toggle-sidebar')"
        class="xl:hidden p-2 rounded-xl text-slate-600 hover:bg-slate-100 transition focus:outline-none focus-visible:ring-4 focus-visible:ring-purple-500/25"
        aria-label="Abrir menú"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
        </svg>
      </button>

      <div class="min-w-0">
        <nav class="flex items-center gap-1.5 text-[11px] sm:text-xs text-slate-500 font-medium" aria-label="Ruta de navegación">
          <span>Pages</span>
          <svg class="w-3 h-3 text-slate-300" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M9 5l7 7-7 7" /></svg>
          <span class="text-purple-700 font-semibold truncate">{{ currentRoute }}</span>
        </nav>
        <!-- Se quitó "capitalize" para respetar preposiciones en minúscula (ej. "de") -->
        <h1 class="text-base sm:text-lg font-bold text-slate-800 leading-tight truncate max-w-[160px] sm:max-w-none">
          {{ currentRoute }}
        </h1>
      </div>
    </div>

    <!-- Buscador y Usuario -->
    <div class="flex items-center space-x-2 sm:space-x-4">
      <!-- Buscador responsivo -->
      <div class="relative">
        <svg class="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <input
          type="text"
          placeholder="Buscar..."
          aria-label="Buscar"
          class="w-32 sm:w-48 md:w-64 h-9 pl-9 pr-3 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-700 placeholder:text-slate-400 hover:border-slate-300 focus:bg-white focus:outline-none focus:ring-4 focus:ring-purple-500/15 focus:border-purple-500 transition"
        />
      </div>

      <!-- Avatar y Nombre -->
      <div class="flex items-center gap-2.5 border-l border-slate-200 pl-2 sm:pl-4">
        <div class="relative">
          <div class="w-8 h-8 sm:w-9 sm:h-9 rounded-full bg-gradient-to-tl from-purple-700 to-pink-500 flex items-center justify-center font-bold text-white text-xs shadow-md shadow-purple-500/30 uppercase ring-2 ring-white">
            {{ userName.slice(0, 2) }}
          </div>
          <span class="absolute -bottom-0.5 -right-0.5 w-2.5 h-2.5 rounded-full bg-emerald-500 ring-2 ring-white"></span>
        </div>
        <span class="text-sm font-semibold text-slate-700 hidden md:inline">{{ userName }}</span>
      </div>
    </div>
  </header>
</template>