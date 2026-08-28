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
</script>

<template>
  <header class="sticky top-4 z-40 bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-3 mb-6 flex items-center justify-between border border-white/40">
    <div>
      <nav class="text-xs text-slate-400 font-medium">
        <span>Pages</span> / <span class="text-slate-700 font-semibold">{{ currentRoute }}</span>
      </nav>
      <h1 class="text-base font-bold text-slate-800 capitalize">{{ currentRoute }}</h1>
    </div>

    <div class="flex items-center space-x-4">
      <!-- Buscador -->
      <div class="relative">
        <input
          type="text"
          placeholder="Buscar pedido, tela o cliente..."
          class="w-64 pl-9 pr-4 py-1.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-700 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition shadow-inner"
        />
        <svg class="w-4 h-4 text-slate-400 absolute left-3 top-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </div>

      <!-- Avatar de Usuario -->
      <div class="flex items-center space-x-2 border-l border-slate-200 pl-4">
        <div class="w-8 h-8 rounded-full bg-slate-200 flex items-center justify-center font-bold text-slate-600 text-xs shadow-inner uppercase">
          {{ userName.slice(0, 2) }}
        </div>
        <span class="text-xs font-semibold text-slate-700 hidden sm:inline">{{ userName }}</span>
      </div>
    </div>
  </header>
</template>