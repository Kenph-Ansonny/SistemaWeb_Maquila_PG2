import { createRouter, createWebHistory } from 'vue-router'
import LoginView from '../views/LoginView.vue'
import DashboardView from '../views/DashboardView.vue'
import { isAuthenticated } from '../utils/auth'

const routes = [
  {
    path: '/',
    name: 'Login',
    component: LoginView,
    meta: { title: 'Iniciar Sesión', public: true }
  },

  {
    path: '/reset-password',
    name: 'ResetPassword',
    component: () => import('../views/ResetPasswordView.vue'),
    meta: { title: 'Restablecer Contraseña', public: true }
  },
  {
    path: '/dashboard',
    name: 'Dashboard',
    component: DashboardView,
    meta: { title: 'Dashboard General' }
  },

  // rutas de los views

  // Rutas de Inventario
  { path: '/inventario/articulos', name: 'Articulos', component: () => import('../views/ArticulosView.vue'), meta: { title: 'Catálogo de Artículos' } },
  { path: '/inventario/almacenes', name: 'Almacenes', component: () => import('../views/AlmacenesView.vue'), meta: { title: 'Almacenes y Bodegas' } },
  { path: '/inventario/unidades', name: 'Unidades', component: () => import('../views/UnidadesView.vue'), meta: { title: 'Unidades y Conversiones' } },
  { path: '/inventario/kardex', name: 'Kardex', component: DashboardView, meta: { title: 'Kardex Transaccional' } },

  // Rutas de Producción
  { path: '/produccion/recetas', name: 'Recetas', component: () => import('../views/RecetasView.vue'), meta: { title: 'Fichas Técnicas (BOM)' } },
  { path: '/produccion/ordenes', name: 'OrdenesProduccion', component: DashboardView, meta: { title: 'Órdenes de Producción' } },
  { path: '/produccion/mermas', name: 'Mermas', component: DashboardView, meta: { title: 'Control de Mermas' } },

  // Rutas de Compras
  { path: '/compras/proveedores', name: 'Proveedores', component: DashboardView, meta: { title: 'Catálogo de Proveedores' } },
  { path: '/compras/registro', name: 'RegistroCompras', component: DashboardView, meta: { title: 'Registro de Compras' } },

  // Rutas de Pedidos
  { path: '/pedidos/clientes', name: 'Clientes', component: DashboardView, meta: { title: 'Catálogo de Clientes' } },
  { path: '/pedidos/lista', name: 'PedidosLista', component: DashboardView, meta: { title: 'Pedidos de Maquila' } },

  // Rutas de Seguridad
  { path: '/seguridad/usuarios', name: 'Usuarios', component: () => import('../views/UsuariosView.vue'), meta: { title: 'Gestión de Usuarios' } },
  { path: '/seguridad/roles', name: 'RolesPermisos', component: () => import('../views/RolesView.vue'), meta: { title: 'Roles y Permisos' }},
  { path: '/seguridad/bitacora', name: 'Bitacora', component: () => import('../views/BitacoraView.vue'), meta: { title: 'Bitácora de Auditoría' }}
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// Guarda global
router.beforeEach((to, from, next) => {
  const esRutaPublica = to.meta.public === true
  const autenticado = isAuthenticated()

  if (!esRutaPublica && !autenticado) {
    return next({ path: '/', query: { sessionExpired: '1' } })
  }

  if (esRutaPublica && to.path === '/' && autenticado) {
    return next('/dashboard')
  }

  next()
})

export default router