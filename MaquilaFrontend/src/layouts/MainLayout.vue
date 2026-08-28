<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import Sidebar from '../components/Sidebar.vue'
import Navbar from '../components/Navbar.vue'

const route = useRoute()

// Simulación de rol del usuario (guardado en localStorage al iniciar sesión)
const userRole = ref(localStorage.getItem('user_role') || 'admin')
const userName = ref(localStorage.getItem('user_name') || 'Admin Maquila')

// Obtiene el título de la página actual desde el meta de la ruta
const currentRouteName = computed(() => route.meta.title || 'Dashboard')
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex">
    <!-- Sidebar Modular -->
    <Sidebar :userRole="userRole" />

    <!-- Contenedor Derecho Dinámico -->
    <div class="flex-1 ml-72 p-6">
      <!-- Navbar Modular -->
      <Navbar :currentRoute="currentRouteName" :userName="userName" />

      <!-- Aquí se renderiza la vista correspondiente del router -->
      <slot></slot>
    </div>
  </div>
</template>