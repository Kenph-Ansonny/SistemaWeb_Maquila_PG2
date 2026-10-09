<script setup>
/**
 * BaseSelect: reemplazo visual del <select> nativo.
 * Uso:
 *   <BaseSelect v-model="valor" :options="[{ value: 1, label: 'Yarda (YD)', hint: 'Longitud', dot: 'bg-purple-500' }]" />
 * - Compatible con v-model (emite update:modelValue con el `value` de la opción).
 * - `hint` (texto secundario a la derecha) y `dot` (clase de color del punto) son opcionales.
 * - Teclado: ↑ ↓ Home End Enter Espacio Esc Tab.
 * - La lista se dibuja en <body>, así que no se recorta dentro de modales con scroll.
 */
import { ref, computed, nextTick, watch, onBeforeUnmount } from 'vue'

const props = defineProps({
  modelValue: { type: [String, Number], default: '' },
  options: { type: Array, default: () => [] },
  placeholder: { type: String, default: 'Seleccionar...' },
  id: { type: String, default: undefined },
  required: { type: Boolean, default: false },
  disabled: { type: Boolean, default: false }
})

const emit = defineEmits(['update:modelValue'])

const open = ref(false)
const activeIndex = ref(-1)
const triggerRef = ref(null)
const panelRef = ref(null)
const panelStyle = ref({})
const listboxId = `bs-${Math.random().toString(36).slice(2, 9)}`

const selectedIndex = computed(() => props.options.findIndex(o => o.value === props.modelValue))
const selected = computed(() => props.options[selectedIndex.value] || null)

const updatePosition = () => {
  const el = triggerRef.value
  if (!el) return
  const r = el.getBoundingClientRect()
  const gap = 6
  const spaceBelow = window.innerHeight - r.bottom - gap - 8
  const spaceAbove = r.top - gap - 8
  const openUp = spaceBelow < 200 && spaceAbove > spaceBelow
  const maxHeight = Math.max(120, Math.min(280, openUp ? spaceAbove : spaceBelow))

  panelStyle.value = {
    left: `${r.left}px`,
    width: `${r.width}px`,
    maxHeight: `${maxHeight}px`,
    ...(openUp
      ? { bottom: `${window.innerHeight - r.top + gap}px` }
      : { top: `${r.bottom + gap}px` })
  }
}

const onDocMouseDown = (e) => {
  if (triggerRef.value?.contains(e.target) || panelRef.value?.contains(e.target)) return
  closePanel()
}

const onScrollOrResize = (e) => {
  if (e?.target && panelRef.value?.contains(e.target)) return
  updatePosition()
}

const addListeners = () => {
  document.addEventListener('mousedown', onDocMouseDown)
  window.addEventListener('scroll', onScrollOrResize, true)
  window.addEventListener('resize', onScrollOrResize)
}

const removeListeners = () => {
  document.removeEventListener('mousedown', onDocMouseDown)
  window.removeEventListener('scroll', onScrollOrResize, true)
  window.removeEventListener('resize', onScrollOrResize)
}

const openPanel = () => {
  if (props.disabled || open.value) return
  activeIndex.value = selectedIndex.value >= 0 ? selectedIndex.value : 0
  updatePosition()
  open.value = true
  addListeners()
}

const closePanel = (focusTrigger = false) => {
  if (!open.value) return
  open.value = false
  removeListeners()
  if (focusTrigger) triggerRef.value?.focus()
}

const choose = (index) => {
  const opt = props.options[index]
  if (!opt) return
  emit('update:modelValue', opt.value)
  closePanel(true)
}

const onKeydown = (e) => {
  const last = props.options.length - 1

  if (!open.value) {
    if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(e.key)) {
      e.preventDefault()
      openPanel()
    }
    return
  }

  switch (e.key) {
    case 'ArrowDown':
      e.preventDefault()
      activeIndex.value = Math.min(last, activeIndex.value + 1)
      break
    case 'ArrowUp':
      e.preventDefault()
      activeIndex.value = Math.max(0, activeIndex.value - 1)
      break
    case 'Home':
      e.preventDefault()
      activeIndex.value = 0
      break
    case 'End':
      e.preventDefault()
      activeIndex.value = last
      break
    case 'Enter':
    case ' ':
      e.preventDefault()
      if (activeIndex.value >= 0) choose(activeIndex.value)
      break
    case 'Escape':
      e.preventDefault()
      e.stopPropagation()
      closePanel(true)
      break
    case 'Tab':
      closePanel(false)
      break
  }
}

// Mantiene visible la opción activa al navegar con teclado
watch(activeIndex, async () => {
  if (!open.value) return
  await nextTick()
  panelRef.value?.querySelector('[data-active="true"]')?.scrollIntoView({ block: 'nearest' })
})

onBeforeUnmount(removeListeners)
</script>

<template>
  <div class="relative">
    <button
      :id="id"
      ref="triggerRef"
      type="button"
      role="combobox"
      aria-haspopup="listbox"
      :aria-expanded="open"
      :aria-controls="listboxId"
      :disabled="disabled"
      @click="open ? closePanel() : openPanel()"
      @keydown="onKeydown"
      :class="[
        'w-full h-10 pl-3 pr-3 flex items-center justify-between gap-2 bg-white border rounded-xl text-sm text-left shadow-xs outline-none transition',
        open
          ? 'border-purple-500 ring-4 ring-purple-500/15'
          : 'border-slate-200 hover:border-slate-300 focus-visible:border-purple-500 focus-visible:ring-4 focus-visible:ring-purple-500/15',
        disabled ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'
      ]"
    >
      <span class="flex items-center gap-2 min-w-0">
        <span v-if="selected?.dot" :class="['w-2 h-2 rounded-full flex-shrink-0', selected.dot]"></span>
        <span :class="['truncate', selected ? 'text-slate-800' : 'text-slate-400']">
          {{ selected ? selected.label : placeholder }}
        </span>
      </span>
      <svg
        :class="['w-4 h-4 flex-shrink-0 transition-transform duration-200', open ? 'rotate-180 text-purple-600' : 'text-slate-400']"
        fill="none" stroke="currentColor" viewBox="0 0 24 24"
      >
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 9l-7 7-7-7" />
      </svg>
    </button>

    <!-- Input invisible: conserva la validación nativa de "campo requerido" -->
    <input
      v-if="required"
      :value="modelValue"
      required
      tabindex="-1"
      aria-hidden="true"
      class="absolute inset-0 w-full h-full opacity-0 pointer-events-none"
    />

    <Teleport to="body">
      <Transition name="bs-pop">
        <div
          v-if="open"
          :id="listboxId"
          ref="panelRef"
          role="listbox"
          :style="panelStyle"
          class="fixed z-[90] overflow-y-auto p-1.5 bg-white border border-slate-200 rounded-xl shadow-xl shadow-slate-900/10 ring-1 ring-black/5"
        >
          <div
            v-for="(opt, i) in options"
            :key="String(opt.value)"
            role="option"
            :aria-selected="i === selectedIndex"
            :data-active="i === activeIndex"
            @mouseenter="activeIndex = i"
            @click="choose(i)"
            :class="[
              'flex items-center justify-between gap-3 px-3 py-2 rounded-lg text-sm cursor-pointer transition-colors',
              i === activeIndex ? 'bg-purple-50 text-purple-800' : 'text-slate-700',
              i === selectedIndex ? 'font-semibold' : 'font-medium'
            ]"
          >
            <span class="flex items-center gap-2 min-w-0">
              <span v-if="opt.dot" :class="['w-2 h-2 rounded-full flex-shrink-0', opt.dot]"></span>
              <span class="truncate">{{ opt.label }}</span>
            </span>
            <span class="flex items-center gap-2 flex-shrink-0">
              <span v-if="opt.hint" class="text-xs font-normal text-slate-500">{{ opt.hint }}</span>
              <svg v-if="i === selectedIndex" class="w-4 h-4 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7" />
              </svg>
            </span>
          </div>

          <div v-if="!options.length" class="px-3 py-3 text-sm text-slate-500 text-center">Sin opciones disponibles</div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.bs-pop-enter-active,
.bs-pop-leave-active {
  transition: opacity 0.12s ease, transform 0.12s ease;
}
.bs-pop-enter-from,
.bs-pop-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
@media (prefers-reduced-motion: reduce) {
  .bs-pop-enter-active,
  .bs-pop-leave-active {
    transition: none;
  }
}
</style>