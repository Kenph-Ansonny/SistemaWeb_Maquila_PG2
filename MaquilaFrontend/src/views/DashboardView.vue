<script setup>
import { ref, computed, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import dashboardService from '../services/dashboardService'
import { usePermissions } from '../composables/usePermissions'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  BarElement,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Filler
} from 'chart.js'
import { Line, Bar } from 'vue-chartjs'

ChartJS.register(
  CategoryScale,
  LinearScale,
  BarElement,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  Filler
)

const { can } = usePermissions()

const isLoading = ref(true)
const periodoActivo = ref('semana') // 'semana' | 'mes' | 'anio'

// Pestaña activa del gráfico comercial: 'ambos' | 'ingresos' | 'pedidos'
const metricaGrafico = ref('ambos')

const kpis = ref({
  totalIngresosPeriodo: 0,
  totalPedidosPeriodo: 0,
  totalPrendasProgramadas: 0,
  ordenesEnTaller: 0
})

const datosGraficoIngresos = ref([])
const datosEmbudo = ref([])

const formatMoneda = (val) =>
  new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(val || 0)

const cargarDashboard = async () => {
  // Verificación de permiso antes de consultar la API
  if (!can('DASHBOARD', 'consultar')) {
    isLoading.value = false
    return
  }

  isLoading.value = true
  try {
    const data = await dashboardService.obtenerResumen(periodoActivo.value)
    kpis.value = data.kpis || kpis.value
    datosGraficoIngresos.value = data.graficoIngresos || []
    datosEmbudo.value = data.embudoProduccion || []
  } catch (error) {
    console.error('Error cargando métricas del dashboard:', error)
  } finally {
    isLoading.value = false
  }
}

const cambiarPeriodo = (nuevoPeriodo) => {
  if (periodoActivo.value === nuevoPeriodo) return
  periodoActivo.value = nuevoPeriodo
  cargarDashboard()
}

onMounted(cargarDashboard)

const esTipoLinea = computed(() => {
  return metricaGrafico.value === 'ingresos' && datosGraficoIngresos.value.length > 1
})

// ---------- Configuración del Gráfico Comercial ----------
const chartComercialData = computed(() => {
  const etiquetas = datosGraficoIngresos.value.map(d => d.etiqueta)
  const ingresos = datosGraficoIngresos.value.map(d => d.ingresos)
  const pedidos = datosGraficoIngresos.value.map(d => d.cantidadPedidos)
  const hayUnSoloPunto = datosGraficoIngresos.value.length === 1

  const datasets = []

  if (metricaGrafico.value === 'ingresos' || metricaGrafico.value === 'ambos') {
    if (esTipoLinea.value) {
      datasets.push({
        type: 'line',
        label: 'Facturación (GTQ)',
        data: ingresos,
        borderColor: '#7c3aed',
        backgroundColor: 'rgba(124, 58, 237, 0.10)',
        borderWidth: 3,
        fill: true,
        tension: 0.35,
        pointBackgroundColor: '#7c3aed',
        pointBorderColor: '#ffffff',
        pointBorderWidth: 2.5,
        pointRadius: 5.5,
        pointHoverRadius: 7.5,
        yAxisID: 'y'
      })
    } else {
      datasets.push({
        type: 'bar',
        label: 'Facturación (GTQ)',
        data: ingresos,
        backgroundColor: 'rgba(124, 58, 237, 0.85)',
        borderColor: '#6d28d9',
        borderWidth: 1.5,
        borderRadius: 8,
        maxBarThickness: hayUnSoloPunto ? 56 : 38,
        categoryPercentage: 0.65,
        barPercentage: 0.85,
        yAxisID: 'y'
      })
    }
  }

  if (metricaGrafico.value === 'pedidos' || metricaGrafico.value === 'ambos') {
    datasets.push({
      type: 'bar',
      label: 'Volumen de Pedidos',
      data: pedidos,
      backgroundColor: 'rgba(236, 72, 153, 0.85)',
      borderColor: '#db2777',
      borderWidth: 1.5,
      borderRadius: 8,
      maxBarThickness: hayUnSoloPunto ? 56 : 38,
      categoryPercentage: 0.65,
      barPercentage: 0.85,
      yAxisID: metricaGrafico.value === 'ambos' ? 'y1' : 'y'
    })
  }

  return { labels: etiquetas, datasets }
})

const chartComercialOptions = computed(() => {
  const esCombinado = metricaGrafico.value === 'ambos'
  const soloPedidos = metricaGrafico.value === 'pedidos'

  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: {
      mode: 'index',
      intersect: false
    },
    plugins: {
      legend: {
        position: 'top',
        align: 'end',
        labels: {
          font: { family: 'inherit', weight: '700', size: 11 },
          color: '#475569',
          usePointStyle: true,
          boxWidth: 8,
          padding: 14
        }
      },
      tooltip: {
        backgroundColor: '#0f172a',
        titleFont: { size: 12, weight: 'bold' },
        bodyFont: { size: 11 },
        padding: 12,
        cornerRadius: 12,
        boxPadding: 4,
        callbacks: {
          label: (context) => {
            if (context.dataset.label.includes('Facturación')) {
              return ` Facturación: ${formatMoneda(context.parsed.y)}`
            }
            return ` Pedidos ingresados: ${context.parsed.y} orden(es)`
          }
        }
      }
    },
    scales: {
      x: {
        grid: { display: false },
        ticks: {
          color: '#64748b',
          font: { size: 11, weight: '700' },
          padding: 8
        }
      },
      y: {
        type: 'linear',
        position: 'left',
        beginAtZero: true,
        grid: { color: '#f1f5f9' },
        ticks: {
          color: soloPedidos ? '#db2777' : '#6d28d9',
          font: { size: 11, weight: '700' },
          maxTicksLimit: 6,
          padding: 8,
          callback: (value) => {
            if (soloPedidos) return `${value} ped.`
            if (value === 0) return 'Q0'
            if (value >= 1000000) return `Q${(value / 1000000).toFixed(1)}M`
            if (value >= 1000) {
              const miles = value / 1000
              return `Q${miles % 1 === 0 ? miles : miles.toFixed(1)}k`
            }
            return `Q${value}`
          }
        }
      },
      y1: {
        display: esCombinado,
        type: 'linear',
        position: 'right',
        beginAtZero: true,
        grid: { display: false },
        ticks: {
          color: '#db2777',
          font: { size: 11, weight: '700' },
          precision: 0,
          maxTicksLimit: 5,
          padding: 8,
          callback: (val) => `${val} ped.`
        }
      }
    }
  }
})

// ---------- Configuración del Embudo de Taller ----------
const chartEmbudoData = computed(() => {
  const etapas = datosEmbudo.value.map(e => e.etapa)
  const cantidades = datosEmbudo.value.map(e => e.totalOrdenes)

  const paleta = {
    'Iniciada': 'rgba(148, 163, 184, 0.85)',
    'En Corte': 'rgba(245, 158, 11, 0.85)',
    'En Costura': 'rgba(99, 102, 241, 0.85)',
    'Terminada': 'rgba(16, 185, 129, 0.85)'
  }

  return {
    labels: etapas,
    datasets: [
      {
        label: 'Órdenes en Taller',
        data: cantidades,
        backgroundColor: etapas.map(e => paleta[e] || '#8b5cf6'),
        borderRadius: 8,
        maxBarThickness: 22
      }
    ]
  }
})

const chartEmbudoOptions = {
  indexAxis: 'y',
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: {
      backgroundColor: '#0f172a',
      cornerRadius: 12,
      padding: 10,
      callbacks: {
        label: (context) => {
          const item = datosEmbudo.value[context.dataIndex]
          return [
            ` Órdenes activas: ${context.parsed.x}`,
            ` Prendas vinculadas: ${item?.totalPrendas || 0} uds`,
            ` Concentración: ${item?.porcentajeDelTotal || 0}% de la planta`
          ]
        }
      }
    }
  },
  scales: {
    x: {
      beginAtZero: true,
      grid: { color: '#f1f5f9' },
      ticks: {
        color: '#64748b',
        precision: 0,
        font: { size: 11, weight: '600' }
      }
    },
    y: {
      grid: { display: false },
      ticks: {
        color: '#1e293b',
        font: { size: 11, weight: '700' }
      }
    }
  }
}
</script>

<template>
  <MainLayout>
    <!-- Restricción de Permiso: Si el rol no puede consultar DASHBOARD -->
    <div v-if="!can('DASHBOARD', 'consultar')" class="bg-white rounded-3xl p-14 text-center shadow-md border border-slate-100">
      <div class="w-16 h-16 rounded-full bg-rose-50 text-rose-500 flex items-center justify-center mx-auto mb-4 ring-8 ring-rose-50/60">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
        </svg>
      </div>
      <h3 class="text-base font-bold text-slate-800">Acceso Restringido</h3>
      <p class="text-sm text-slate-500 mt-1.5 max-w-sm mx-auto">
        No cuentas con autorización para consultar el panel de control e indicadores financieros de la planta.
      </p>
    </div>

    <!-- Contenido del Dashboard para Usuarios Autorizados -->
    <div v-else class="space-y-6">
      <!-- Encabezado con Selector de Periodo -->
      <div class="relative overflow-hidden bg-white/80 backdrop-blur-md shadow-md rounded-2xl px-6 py-5 border border-white/40 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="absolute inset-y-0 left-0 w-1.5 bg-gradient-to-b from-purple-700 to-pink-500"></div>
        <div class="pl-2">
          <h2 class="text-xl font-black text-slate-900 tracking-tight">Panel de Inteligencia Operativa</h2>
          <p class="text-xs text-slate-500 font-semibold mt-0.5">Control de facturación, balance de pedidos y cuellos de botella en producción textil</p>
        </div>

        <div class="inline-flex p-1 bg-slate-100 rounded-xl border border-slate-200 self-start sm:self-auto">
          <button
            v-for="op in [{ k: 'semana', l: 'Semanas' }, { k: 'mes', l: 'Meses' }, { k: 'anio', l: 'Años' }]"
            :key="op.k"
            @click="cambiarPeriodo(op.k)"
            :class="[
              'px-4 py-1.5 text-xs font-bold rounded-lg transition-all',
              periodoActivo === op.k
                ? 'bg-gradient-to-tl from-purple-700 to-pink-500 text-white shadow-sm'
                : 'text-slate-600 hover:text-slate-900'
            ]"
          >
            {{ op.l }}
          </button>
        </div>
      </div>

      <!-- Tarjetas KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 flex items-center justify-between hover:shadow-lg transition-all">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Ingresos Facturados</p>
            <p class="text-2xl font-black text-slate-900 mt-1 font-mono tracking-tight">
              {{ formatMoneda(kpis.totalIngresosPeriodo) }}
            </p>
            <span class="text-[10px] font-bold text-purple-700 bg-purple-50 px-2 py-0.5 rounded-full inline-block mt-1">
              Período: {{ periodoActivo }}
            </span>
          </div>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-tl from-purple-700 to-pink-500 text-white flex items-center justify-center shadow-md shadow-purple-500/25 shrink-0">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 flex items-center justify-between hover:shadow-lg transition-all">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Pedidos Concretados</p>
            <p class="text-2xl font-black text-purple-700 mt-1 font-mono">
              {{ kpis.totalPedidosPeriodo }} <span class="text-sm font-semibold text-slate-400">ped.</span>
            </p>
            <span class="text-[10px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full inline-block mt-1">
              Vigentes / No cancelados
            </span>
          </div>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-tl from-indigo-600 to-purple-600 text-white flex items-center justify-center shadow-md shadow-indigo-500/25 shrink-0">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 flex items-center justify-between hover:shadow-lg transition-all">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Prendas a Fabricar</p>
            <p class="text-2xl font-black text-indigo-600 mt-1 font-mono">
              {{ Number(kpis.totalPrendasProgramadas).toLocaleString('es-GT') }} <span class="text-sm font-semibold text-slate-400">uds</span>
            </p>
            <span class="text-[10px] font-bold text-indigo-700 bg-indigo-50 px-2 py-0.5 rounded-full inline-block mt-1">
              Volumen acumulado
            </span>
          </div>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-tl from-pink-500 to-rose-500 text-white flex items-center justify-center shadow-md shadow-pink-500/25 shrink-0">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z" />
            </svg>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-4 shadow-md border border-slate-100 flex items-center justify-between hover:shadow-lg transition-all">
          <div>
            <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Órdenes en Proceso</p>
            <p class="text-2xl font-black text-emerald-600 mt-1 font-mono">
              {{ kpis.ordenesEnTaller }} <span class="text-sm font-semibold text-slate-400">activas</span>
            </p>
            <span class="text-[10px] font-bold text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full inline-block mt-1">
              Corte y Costura en vivo
            </span>
          </div>
          <div class="w-12 h-12 rounded-xl bg-gradient-to-tl from-emerald-500 to-teal-500 text-white flex items-center justify-center shadow-md shadow-emerald-500/25 shrink-0">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
          </div>
        </div>
      </div>

      <!-- Sección de Gráficos -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Gráfica 1: Evolución Comercial -->
        <div class="lg:col-span-8 bg-white rounded-2xl p-6 shadow-md border border-slate-100 flex flex-col justify-between">
          <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between pb-3 border-b border-slate-100 gap-3">
            <div>
              <h3 class="text-base font-black text-slate-900">Evolución Comercial</h3>
              <p class="text-xs text-slate-500 font-medium">Comportamiento financiero y volumen de pedidos en el tiempo</p>
            </div>

            <!-- Selector de Métrica Interno -->
            <div class="inline-flex p-0.5 bg-slate-100 rounded-lg border border-slate-200 self-start sm:self-auto">
              <button
                v-for="m in [{ k: 'ambos', l: 'Comparativa' }, { k: 'ingresos', l: 'Facturación' }, { k: 'pedidos', l: 'Pedidos' }]"
                :key="m.k"
                @click="metricaGrafico = m.k"
                :class="[
                  'px-3 py-1 text-[11px] font-bold rounded-md transition-all',
                  metricaGrafico === m.k
                    ? 'bg-white text-purple-700 shadow-xs'
                    : 'text-slate-500 hover:text-slate-800'
                ]"
              >
                {{ m.l }}
              </button>
            </div>
          </div>

          <div class="h-[310px] w-full relative mt-4">
            <div v-if="isLoading" class="absolute inset-0 flex items-center justify-center bg-white/70 backdrop-blur-xs z-10">
              <span class="text-xs font-bold text-purple-600 animate-pulse">Sincronizando serie temporal...</span>
            </div>
            
            <Line
              v-if="datosGraficoIngresos.length > 0 && esTipoLinea"
              :data="chartComercialData"
              :options="chartComercialOptions"
            />
            <Bar
              v-else-if="datosGraficoIngresos.length > 0"
              :data="chartComercialData"
              :options="chartComercialOptions"
            />
            
            <div v-else-if="!isLoading" class="h-full flex items-center justify-center text-xs text-slate-400 italic">
              No hay pedidos registrados en el intervalo seleccionado.
            </div>
          </div>
        </div>

        <!-- Gráfica 2: Embudo de Taller -->
        <div class="lg:col-span-4 bg-white rounded-2xl p-6 shadow-md border border-slate-100 flex flex-col justify-between">
          <div class="pb-3 border-b border-slate-100">
            <h3 class="text-base font-black text-slate-900">Embudo de Taller</h3>
            <p class="text-xs text-slate-500 font-medium">Identificación de cuellos de botella por etapa</p>
          </div>

          <div class="h-[220px] w-full relative mt-2">
            <div v-if="isLoading" class="absolute inset-0 flex items-center justify-center bg-white/70 backdrop-blur-xs z-10">
              <span class="text-xs font-bold text-indigo-600 animate-pulse">Analizando órdenes...</span>
            </div>
            <Bar
              v-if="datosEmbudo.length > 0"
              :data="chartEmbudoData"
              :options="chartEmbudoOptions"
            />
            <div v-else-if="!isLoading" class="h-full flex items-center justify-center text-xs text-slate-400 italic">
              No hay órdenes de producción en planta.
            </div>
          </div>

          <div class="pt-4 border-t border-slate-100 grid grid-cols-2 gap-2.5">
            <div class="bg-amber-50/80 p-2.5 rounded-xl border border-amber-200">
              <span class="text-[10px] font-bold text-amber-800 uppercase tracking-wider block">Mesa de Corte</span>
              <p class="font-mono font-black text-amber-900 text-sm mt-0.5">
                {{ datosEmbudo.find(e => e.etapa === 'En Corte')?.totalOrdenes || 0 }} <span class="text-xs font-semibold">órdenes</span>
              </p>
            </div>
            <div class="bg-indigo-50/80 p-2.5 rounded-xl border border-indigo-200">
              <span class="text-[10px] font-bold text-indigo-800 uppercase tracking-wider block">Línea de Costura</span>
              <p class="font-mono font-black text-indigo-900 text-sm mt-0.5">
                {{ datosEmbudo.find(e => e.etapa === 'En Costura')?.totalOrdenes || 0 }} <span class="text-xs font-semibold">órdenes</span>
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </MainLayout>
</template>