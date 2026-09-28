import type { Component } from 'vue'
import {
  Bookmark,
  Crosshair,
  House,
  LayoutDashboard,
  Radar,
  ScanSearch,
  Settings,
} from '@lucide/vue'
import type { MessageSchema } from '@/app/i18n'

export type NavKey =
  'dashboard' | 'homes' | 'bookmarks' | 'matches' | 'watches' | 'scans' | 'settings'

export interface NavItem {
  key: NavKey
  /** Route name; also the route path segment except for the dashboard. */
  routeName: NavKey
  to: string
  labelKey: `nav.${keyof MessageSchema['nav']}`
  icon: Component
}

export const NAV_ITEMS: readonly NavItem[] = [
  {
    key: 'dashboard',
    routeName: 'dashboard',
    to: '/',
    labelKey: 'nav.dashboard',
    icon: LayoutDashboard,
  },
  { key: 'homes', routeName: 'homes', to: '/homes', labelKey: 'nav.homes', icon: House },
  {
    key: 'bookmarks',
    routeName: 'bookmarks',
    to: '/bookmarks',
    labelKey: 'nav.bookmarks',
    icon: Bookmark,
  },
  {
    key: 'matches',
    routeName: 'matches',
    to: '/matches',
    labelKey: 'nav.matches',
    icon: Crosshair,
  },
  { key: 'watches', routeName: 'watches', to: '/watches', labelKey: 'nav.watches', icon: Radar },
  { key: 'scans', routeName: 'scans', to: '/scans', labelKey: 'nav.scans', icon: ScanSearch },
  {
    key: 'settings',
    routeName: 'settings',
    to: '/settings',
    labelKey: 'nav.settings',
    icon: Settings,
  },
]

/** The four destinations pinned to the mobile bottom bar; the fifth slot opens "More". */
export const MOBILE_PRIMARY_KEYS: readonly NavKey[] = ['dashboard', 'homes', 'matches', 'watches']

export const MOBILE_PRIMARY_ITEMS = NAV_ITEMS.filter((item) =>
  MOBILE_PRIMARY_KEYS.includes(item.key),
)
export const MOBILE_MORE_ITEMS = NAV_ITEMS.filter((item) => !MOBILE_PRIMARY_KEYS.includes(item.key))

/** Which top-level section a route path belongs to (detail pages highlight their parent). */
export function sectionForPath(path: string): NavKey | null {
  if (path === '/') return 'dashboard'
  if (path.startsWith('/listings')) return 'homes'
  const match = NAV_ITEMS.find(
    (item) => item.to !== '/' && (path === item.to || path.startsWith(`${item.to}/`)),
  )
  return match?.key ?? null
}
