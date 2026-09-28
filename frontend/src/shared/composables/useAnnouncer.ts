import { nextTick, readonly, ref } from 'vue'

export type Politeness = 'polite' | 'assertive'

const polite = ref('')
const assertive = ref('')

/**
 * Screen-reader announcements through the app-wide aria-live regions rendered by
 * <LiveAnnouncer>. Clearing first makes repeated identical messages announce again.
 */
export function useAnnouncer() {
  async function announce(message: string, politeness: Politeness = 'polite') {
    const target = politeness === 'assertive' ? assertive : polite
    target.value = ''
    await nextTick()
    target.value = message
  }

  function clear() {
    polite.value = ''
    assertive.value = ''
  }

  return {
    polite: readonly(polite),
    assertive: readonly(assertive),
    announce,
    clear,
  }
}
