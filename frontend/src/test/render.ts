import { type Component, type DefineComponent, defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { createMemoryHistory, createRouter, type RouteRecordRaw, type Router } from 'vue-router'
import { createAppI18n } from '@/app/i18n'
import type { AppLocale } from '@/shared/lib/locale'

const Blank = defineComponent({
  name: 'BlankView',
  render: () => h('div', { 'data-testid': 'blank-view' }),
})

export const testRoutes: RouteRecordRaw[] = [
  { path: '/', name: 'dashboard', component: Blank },
  { path: '/login', name: 'login', component: Blank, meta: { public: true, layout: 'bare' } },
  { path: '/homes', name: 'homes', component: Blank },
  { path: '/bookmarks', name: 'bookmarks', component: Blank },
  { path: '/matches', name: 'matches', component: Blank },
  { path: '/watches', name: 'watches', component: Blank },
  { path: '/watches/:id', name: 'watch', component: Blank },
  { path: '/scans', name: 'scans', component: Blank },
  { path: '/settings', name: 'settings', component: Blank },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: Blank },
]

export interface RenderOptions {
  route?: string
  routes?: RouteRecordRaw[]
  locale?: AppLocale
  queryClient?: QueryClient
  props?: Record<string, unknown>
  slots?: Record<string, () => unknown>
}

export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  })
}

/** Mount with Pinia, i18n, vue-query and a memory router already on `route`. */
export async function renderWithPlugins(component: Component, options: RenderOptions = {}) {
  const { route = '/', routes = testRoutes, locale = 'en', queryClient, props, slots } = options
  const pinia = createPinia()
  setActivePinia(pinia)
  const i18n = createAppI18n(locale)
  const client = queryClient ?? createTestQueryClient()
  const router: Router = createRouter({ history: createMemoryHistory(), routes })
  await router.push(route)
  await router.isReady()

  const wrapper = mount(component as DefineComponent, {
    attachTo: document.body,
    props,
    slots,
    global: {
      plugins: [pinia, i18n, router, [VueQueryPlugin, { queryClient: client }]],
    },
  })

  return { wrapper, router, i18n, queryClient: client, pinia }
}

export function flushPromises(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0))
}
