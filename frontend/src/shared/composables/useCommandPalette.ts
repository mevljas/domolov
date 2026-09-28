import { ref } from 'vue'

const open = ref(false)

/** Shared open state so any trigger (header button, shortcut) controls the one palette. */
export function useCommandPalette() {
  return {
    open,
    show: () => (open.value = true),
    hide: () => (open.value = false),
    toggle: () => (open.value = !open.value),
  }
}

/** Ctrl+K / Cmd+K — ignores key repeats. */
export function isCommandPaletteShortcut(event: KeyboardEvent): boolean {
  return (
    !event.repeat &&
    (event.ctrlKey || event.metaKey) &&
    !event.altKey &&
    event.key.toLowerCase() === 'k'
  )
}

export function isApplePlatform(): boolean {
  if (typeof navigator === 'undefined') return false
  const platform =
    (navigator as Navigator & { userAgentData?: { platform?: string } }).userAgentData?.platform ??
    navigator.platform ??
    ''
  return /mac|iphone|ipad|ipod/i.test(platform)
}
