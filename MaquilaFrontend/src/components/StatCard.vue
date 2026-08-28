<script setup>
defineProps({
  title: String,
  value: String,
  diff: String,
  positive: {
    type: Boolean,
    default: true
  },
  iconBg: {
    type: String,
    default: 'bg-gradient-to-tl from-purple-700 to-pink-500'
  },
  icon: String
})
</script>

<template>
  <div class="bg-white rounded-2xl p-4 shadow-md flex items-center justify-between border border-slate-100">
    <div>
      <p class="text-xs font-bold text-slate-400 uppercase tracking-wider">{{ title }}</p>
      <h3 class="text-lg font-extrabold text-slate-800 mt-1">
        {{ value }}
        <span :class="['text-xs font-bold ml-1', positive ? 'text-emerald-500' : 'text-red-500']">
          {{ diff }}
        </span>
      </h3>
    </div>
    <div :class="['w-12 h-12 rounded-xl flex items-center justify-center text-white shadow-md', iconBg]">
      <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="icon" />
      </svg>
    </div>
  </div>
</template>