<script setup>
import MainLayout from '../layouts/MainLayout.vue'
import StatCard from '../components/StatCard.vue'

const cards = [
  { title: "Producción Diaria", value: "2,450 pzs", diff: "+12%", positive: true, iconBg: "bg-gradient-to-tl from-purple-700 to-pink-500", icon: "M13 10V3L4 14h7v7l9-11h-7z" },
  { title: "Materia Prima (Rollos)", value: "380", diff: "+3%", positive: true, iconBg: "bg-gradient-to-tl from-zinc-800 to-zinc-700", icon: "M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" },
  { title: "Pedidos en Proceso", value: "18", diff: "-2%", positive: false, iconBg: "bg-gradient-to-tl from-blue-600 to-cyan-400", icon: "M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" },
  { title: "Entregas del Mes", value: "Q 145,200", diff: "+8%", positive: true, iconBg: "bg-gradient-to-tl from-emerald-500 to-teal-400", icon: "M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" },
]
</script>

<template>
  <MainLayout>
    <!-- Diferentes Métricas -->
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-6 mb-6">
      <StatCard 
        v-for="(card, index) in cards" 
        :key="index"
        :title="card.title"
        :value="card.value"
        :diff="card.diff"
        :positive="card.positive"
        :iconBg="card.iconBg"
        :icon="card.icon"
      />
    </div>

    <!-- Área de trabajo del Dashboard -->
    <div class="bg-white rounded-2xl p-6 shadow-md border border-slate-100 min-h-[450px]">
      <h2 class="text-base font-bold text-slate-800 mb-2">Resumen General de Maquila</h2>
      <p class="text-sm text-slate-500">
        Aquí se integrarán las gráficas de producción y los reportes de rendimiento.
      </p>
    </div>
  </MainLayout>
</template>