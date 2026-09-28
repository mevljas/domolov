import { nextTick } from 'vue'
import type { Router } from 'vue-router'
import type { Translate } from '@/api/problem'
import { useAnnouncer } from '@/shared/composables/useAnnouncer'

/**
 * After each navigation: update document.title, announce the new page to screen readers
 * and move focus to <main> (skipped on the initial load so the page does not jump).
 */
export function installRouteEffects(router: Router, t: Translate) {
  const { announce } = useAnnouncer()
  let initial = true

  const pageName = (key: string | undefined) => (key ? t(key) : t('common.appName'))

  const refreshTitle = () => {
    const page = pageName(router.currentRoute.value.meta.titleKey)
    const app = t('common.appName')
    document.title = page === app ? app : `${page} · ${app}`
  }

  router.afterEach(async (to, _from, failure) => {
    if (failure) return
    refreshTitle()
    if (initial) {
      initial = false
      return
    }
    await nextTick()
    void announce(t('a11y.navigatedTo', { page: pageName(to.meta.titleKey) }))
    document.getElementById('main')?.focus({ preventScroll: true })
  })

  return { refreshTitle }
}
