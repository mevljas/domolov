import { computed } from 'vue'
import { useLocalStorage, useMediaQuery } from '@vueuse/core'

const STORAGE_KEY = 'domolov:sidebar-collapsed'

/**
 * Sidebar collapse state. Tablets (md–lg) default to the icon rail, desktops to the full
 * sidebar; an explicit toggle is remembered across sessions.
 */
export function useSidebar() {
  const explicit = useLocalStorage<boolean | null>(STORAGE_KEY, null, {
    serializer: {
      read: (raw) => (raw === 'true' ? true : raw === 'false' ? false : null),
      write: (value) => String(value),
    },
  })
  const isTablet = useMediaQuery('(min-width: 768px) and (max-width: 1023.98px)')
  const collapsed = computed(() => explicit.value ?? isTablet.value)

  function toggle() {
    explicit.value = !collapsed.value
  }

  return { collapsed, toggle }
}
