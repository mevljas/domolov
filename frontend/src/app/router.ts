import type { QueryClient } from '@tanstack/vue-query'
import {
  createRouter,
  createWebHistory,
  type RouteLocationNormalized,
  type RouteRecordRaw,
  type Router,
  type RouterHistory,
} from 'vue-router'
import { ensureSession, sessionQueryKey, type Session } from '@/features/auth/useSession'
import { loginLocation, safeRedirect } from '@/shared/lib/redirect'

declare module 'vue-router' {
  interface RouteMeta {
    /** Reachable without a session. */
    public?: boolean
    /** `bare` renders without the app shell (login). */
    layout?: 'app' | 'bare'
    /** i18n key for document.title and route announcements. */
    titleKey?: string
  }
}

export const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/features/auth/LoginView.vue'),
    meta: { public: true, layout: 'bare', titleKey: 'auth.title' },
  },
  {
    path: '/',
    name: 'dashboard',
    component: () => import('@/features/dashboard/DashboardView.vue'),
    meta: { titleKey: 'dashboard.title' },
  },
  {
    path: '/watches',
    name: 'watches',
    component: () => import('@/features/watches/WatchesView.vue'),
    meta: { titleKey: 'watches.title' },
  },
  {
    path: '/watches/:id',
    name: 'watch',
    component: () => import('@/features/watches/WatchDetailView.vue'),
    props: true,
    meta: { titleKey: 'watches.detailTitle' },
  },
  {
    path: '/homes',
    name: 'homes',
    component: () => import('@/features/homes/HomesView.vue'),
    meta: { titleKey: 'homes.title' },
  },
  {
    path: '/homes/:id',
    name: 'home',
    component: () => import('@/features/homes/HomeDetailView.vue'),
    props: true,
    meta: { titleKey: 'homes.detailTitle' },
  },
  {
    path: '/listings/:id',
    name: 'listing',
    component: () => import('@/features/homes/ListingDetailView.vue'),
    props: true,
    meta: { titleKey: 'listings.detailTitle' },
  },
  {
    path: '/bookmarks',
    name: 'bookmarks',
    component: () => import('@/features/bookmarks/BookmarksView.vue'),
    meta: { titleKey: 'bookmarks.title' },
  },
  {
    path: '/matches',
    name: 'matches',
    component: () => import('@/features/matches/MatchesView.vue'),
    meta: { titleKey: 'matches.title' },
  },
  {
    path: '/scans',
    name: 'scans',
    component: () => import('@/features/scans/ScansView.vue'),
    meta: { titleKey: 'scans.title' },
  },
  {
    path: '/settings',
    name: 'settings',
    component: () => import('@/features/settings/SettingsView.vue'),
    meta: { titleKey: 'settings.title' },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('./NotFoundView.vue'),
    meta: { titleKey: 'notFound.title' },
  },
]

/** Resolve where a navigation may go given the current session state. */
export async function authGuard(to: RouteLocationNormalized, queryClient: QueryClient) {
  if (to.meta.public) {
    if (to.name === 'login' && queryClient.getQueryData<Session | null>(sessionQueryKey)) {
      return safeRedirect(to.query.redirect)
    }
    return true
  }

  try {
    if (await ensureSession(queryClient)) return true
  } catch {
    // Backend unreachable: keep a previously confirmed session (offline PWA), else sign in.
    if (queryClient.getQueryData<Session | null>(sessionQueryKey)) return true
  }
  return loginLocation(to.fullPath)
}

export function createAppRouter(
  queryClient: QueryClient,
  history: RouterHistory = createWebHistory(),
): Router {
  const router = createRouter({
    history,
    routes,
    scrollBehavior(to, _from, savedPosition) {
      if (savedPosition) return savedPosition
      if (to.hash) return { el: to.hash, top: 80 }
      return { top: 0 }
    },
  })

  router.beforeEach((to) => authGuard(to, queryClient))

  return router
}
