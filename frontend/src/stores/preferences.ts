import { computed } from 'vue'
import { defineStore } from 'pinia'
import { useLocalStorage } from '@vueuse/core'
import { type AppLocale, DEFAULT_LOCALE, isAppLocale } from '@/shared/lib/locale'

export type ThemePreference = 'system' | 'light' | 'dark'

export const THEME_PREFERENCES: readonly ThemePreference[] = ['system', 'light', 'dark']

export const PREFERENCES_STORAGE_KEY = 'domolov:preferences'

interface StoredPreferences {
  theme: ThemePreference
  locale: AppLocale
}

function isTheme(value: unknown): value is ThemePreference {
  return typeof value === 'string' && (THEME_PREFERENCES as readonly string[]).includes(value)
}

/** Client-only UI preferences, persisted to localStorage. Server state lives in vue-query. */
export const usePreferencesStore = defineStore('preferences', () => {
  const stored = useLocalStorage<StoredPreferences>(
    PREFERENCES_STORAGE_KEY,
    { theme: 'system', locale: DEFAULT_LOCALE },
    {
      mergeDefaults: (storage, defaults) => ({
        theme: isTheme(storage?.theme) ? storage.theme : defaults.theme,
        locale: isAppLocale(storage?.locale) ? storage.locale : defaults.locale,
      }),
    },
  )

  function setTheme(theme: ThemePreference) {
    stored.value = { ...stored.value, theme }
  }

  function setLocale(locale: AppLocale) {
    stored.value = { ...stored.value, locale }
  }

  return {
    theme: computed(() => stored.value.theme),
    locale: computed(() => stored.value.locale),
    setTheme,
    setLocale,
  }
})
