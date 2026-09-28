import { computed } from 'vue'
import { useOnline as useVueUseOnline } from '@vueuse/core'

export function useOnline() {
  const isOnline = useVueUseOnline()
  return {
    isOnline,
    isOffline: computed(() => !isOnline.value),
  }
}
