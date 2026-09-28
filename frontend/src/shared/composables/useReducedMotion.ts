import { computed } from 'vue'
import { usePreferredReducedMotion } from '@vueuse/core'

/** `true` when the user asked the OS to minimise motion. */
export function useReducedMotion() {
  const preference = usePreferredReducedMotion()
  return computed(() => preference.value === 'reduce')
}

export function prefersReducedMotion(): boolean {
  return (
    typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
  )
}
