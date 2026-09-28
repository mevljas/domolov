import { computed, nextTick } from 'vue'
import type { Router } from 'vue-router'
import { prefersReducedMotion, useReducedMotion } from './useReducedMotion'

export function supportsViewTransitions(): boolean {
  return typeof document !== 'undefined' && typeof document.startViewTransition === 'function'
}

/** Run a DOM update inside a View Transition when supported and motion is allowed. */
export function useViewTransition() {
  const reduced = useReducedMotion()
  const isSupported = supportsViewTransitions()
  const isEnabled = computed(() => isSupported && !reduced.value)

  async function run(update: () => void | Promise<void>): Promise<void> {
    if (!isEnabled.value) {
      await update()
      return
    }
    await document.startViewTransition(update).updateCallbackDone
  }

  return { isSupported, isEnabled, run }
}

/**
 * Wrap router navigations in document.startViewTransition: the old view is snapshotted in
 * beforeResolve, and the transition's update callback resolves after the new route renders.
 */
export function installRouteViewTransitions(
  router: Router,
  isEnabled: () => boolean = () => supportsViewTransitions() && !prefersReducedMotion(),
): () => void {
  let finish: (() => void) | null = null
  const settle = () => {
    finish?.()
    finish = null
  }

  const removeBefore = router.beforeResolve((to, from) => {
    if (!isEnabled() || from.matched.length === 0 || to.path === from.path) return
    settle()
    const rendered = new Promise<void>((resolve) => {
      finish = resolve
    })
    return new Promise<void>((resolve) => {
      const transition = document.startViewTransition(async () => {
        resolve()
        await rendered
      })
      transition.ready.catch(settle)
    })
  })

  const removeAfter = router.afterEach(async () => {
    await nextTick()
    settle()
  })

  const removeError = router.onError(settle)

  return () => {
    removeBefore()
    removeAfter()
    removeError()
    settle()
  }
}
