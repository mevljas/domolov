import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { type AppLocale, SUPPORTED_LOCALES } from '@/shared/lib/locale'
import { usePreferencesStore } from '@/stores/preferences'

export function useLocale() {
  const store = usePreferencesStore()
  const locale = computed<AppLocale>(() => store.locale)

  function setLocale(next: AppLocale) {
    store.setLocale(next)
  }

  function toggleLocale() {
    const index = SUPPORTED_LOCALES.indexOf(locale.value)
    setLocale(SUPPORTED_LOCALES[(index + 1) % SUPPORTED_LOCALES.length]!)
  }

  return { locale, locales: SUPPORTED_LOCALES, setLocale, toggleLocale }
}

/** Push the preferred locale into vue-i18n and <html lang>; call once from the app root. */
export function useLocaleEffect() {
  const i18n = useI18n({ useScope: 'global' })
  const state = useLocale()
  watch(
    state.locale,
    (value) => {
      i18n.locale.value = value
      document.documentElement.lang = value
    },
    { immediate: true },
  )
  return state
}
