import { computed, watch } from 'vue'
import { usePreferredDark } from '@vueuse/core'
import { THEME_PREFERENCES, type ThemePreference, usePreferencesStore } from '@/stores/preferences'

export type ResolvedTheme = 'light' | 'dark'

/** Browser chrome colour per theme (matches --background). */
export const THEME_COLORS: Record<ResolvedTheme, string> = {
  light: '#F7F5F0',
  dark: '#0F1F18',
}

export function applyTheme(
  theme: ResolvedTheme,
  root: HTMLElement = document.documentElement,
): void {
  root.classList.toggle('dark', theme === 'dark')
  root.style.colorScheme = theme
  const meta = document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')
  meta?.setAttribute('content', THEME_COLORS[theme])
}

export function useTheme() {
  const store = usePreferencesStore()
  const prefersDark = usePreferredDark()

  const preference = computed<ThemePreference>(() => store.theme)
  const resolved = computed<ResolvedTheme>(() => {
    if (preference.value === 'system') return prefersDark.value ? 'dark' : 'light'
    return preference.value
  })
  const isDark = computed(() => resolved.value === 'dark')

  function setTheme(theme: ThemePreference) {
    store.setTheme(theme)
  }

  /** system → light → dark → system */
  function cycleTheme() {
    const index = THEME_PREFERENCES.indexOf(preference.value)
    setTheme(THEME_PREFERENCES[(index + 1) % THEME_PREFERENCES.length]!)
  }

  /** Flip between light and dark based on what is currently shown. */
  function toggleTheme() {
    setTheme(isDark.value ? 'light' : 'dark')
  }

  return { preference, resolved, isDark, setTheme, cycleTheme, toggleTheme }
}

/** Keep <html class="dark"> and theme-color in sync; call once from the app root. */
export function useThemeEffect() {
  const theme = useTheme()
  watch(theme.resolved, (value) => applyTheme(value), { immediate: true })
  return theme
}
