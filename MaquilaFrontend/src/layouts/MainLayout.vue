<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import Sidebar from '../components/Sidebar.vue'
import Navbar from '../components/Navbar.vue'

const route = useRoute()

const isSidebarOpen = ref(false)
const userRole = ref(localStorage.getItem('user_role') || 'admin')
const userName = ref(localStorage.getItem('user_name') || 'Admin Maquila')

const currentRouteName = computed(() => route.meta.title || 'Dashboard')
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex">
    <!-- Sidebar con estado y evento para cerrar -->
    <Sidebar 
      :userRole="userRole" 
      :isOpen="isSidebarOpen"
      @close="isSidebarOpen = false" 
    />

    <!-- Contenedor adaptativo: Margen 0 en móvil, margen a la izquierda en pantallas grandes (xl:ml-72) -->
    <div class="flex-1 w-full xl:ml-72 p-4 sm:p-6 transition-all duration-300">
      <Navbar 
        :currentRoute="currentRouteName" 
        :userName="userName" 
        @toggle-sidebar="isSidebarOpen = !isSidebarOpen"
      />

      <slot></slot>
    </div>
  </div>
</template>