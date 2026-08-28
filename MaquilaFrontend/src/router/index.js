import { createRouter, createWebHistory } from 'vue-router'
import LoginView from '../views/LoginView.vue'
import DashboardView from '../views/DashboardView.vue'

const routes = [
  { 
    path: '/', 
    name: 'Login', 
    component: LoginView,
    meta: { title: 'Iniciar Sesión' }
  },
  { 
    path: '/dashboard', 
    name: 'Dashboard', 
    component: DashboardView,
    meta: { title: 'Dashboard General' }
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router