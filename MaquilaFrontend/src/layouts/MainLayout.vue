<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import Sidebar from '../components/Sidebar.vue'
import Navbar from '../components/Navbar.vue'

const route = useRoute()

const isSidebarOpen = ref(false)
const isSidebarCollapsed = ref(false)
const userRole = ref(localStorage.getItem('user_role') || 'admin')
const userName = ref(localStorage.getItem('user_name') || 'Admin Maquila')

const currentRouteName = computed(() => route.meta.title || 'Dashboard')
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex">
    <!-- Sidebar con binding bidireccional de colapso -->
    <Sidebar 
      :userRole="userRole" 
      :isOpen="isSidebarOpen"
      v-model:isCollapsed="isSidebarCollapsed"
      @close="isSidebarOpen = false" 
    />

    <!-- Contenedor adaptativo:
         - En móvil/tablet: ml-0 (sin margen fijo)
         - En escritorio (xl): xl:ml-28 (colapsado) / xl:ml-72 (expandido) -->
    <div 
      :class="[
        'flex-1 w-full min-h-screen flex flex-col p-4 sm:p-6 transition-all duration-300 ease-in-out',
        isSidebarCollapsed ? 'xl:ml-28' : 'xl:ml-72'
      ]"
    >
      <Navbar 
        :currentRoute="currentRouteName" 
        :userName="userName" 
        @toggle-sidebar="isSidebarOpen = !isSidebarOpen"
      />

      <!-- Espacio principal del contenido -->
      <main class="flex-1 mt-4">
        <slot></slot>
      </main>
    </div>
  </div>
</template>