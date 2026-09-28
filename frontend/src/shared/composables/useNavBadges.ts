import { computed, reactive, readonly } from 'vue'
import type { NavKey } from '@/shared/navigation'

const counts = reactive<Partial<Record<NavKey, number>>>({})

/** Counts shown next to navigation items (e.g. unseen Matches). Features call setBadge. */
export function useNavBadges() {
  function setBadge(key: NavKey, count: number | null | undefined) {
    if (!count || count < 0) delete counts[key]
    else counts[key] = Math.floor(count)
  }

  function badgeFor(key: NavKey) {
    return computed(() => counts[key] ?? 0)
  }

  return { badges: readonly(counts), setBadge, badgeFor }
}

/** "99+" style label for compact badges. */
export function formatBadge(count: number, max = 99): string {
  return count > max ? `${max}+` : String(count)
}
