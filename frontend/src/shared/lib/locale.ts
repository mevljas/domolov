export const SUPPORTED_LOCALES = ['sl', 'en'] as const

export type AppLocale = (typeof SUPPORTED_LOCALES)[number]

export const DEFAULT_LOCALE: AppLocale = 'sl'

/** BCP 47 tag used for Intl formatting. English uses en-GB for day-first dates and 24h time. */
const INTL_TAGS: Record<AppLocale, string> = {
  sl: 'sl-SI',
  en: 'en-GB',
}

export function isAppLocale(value: unknown): value is AppLocale {
  return typeof value === 'string' && (SUPPORTED_LOCALES as readonly string[]).includes(value)
}

export function intlLocale(locale: AppLocale): string {
  return INTL_TAGS[locale]
}
