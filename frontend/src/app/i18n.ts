import { createI18n } from 'vue-i18n'
import en from '@/i18n/locales/en.json'
import sl from '@/i18n/locales/sl.json'
import { type AppLocale, DEFAULT_LOCALE } from '@/shared/lib/locale'

export type MessageSchema = typeof en

declare module 'vue-i18n' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  export interface DefineLocaleMessage extends MessageSchema {}
}

/** Slovenian has four plural forms (one, two, few, other); index 0 is the optional zero form. */
function slovenianPlural(choice: number, choicesLength: number): number {
  const n = Math.abs(choice)
  const hasZero = choicesLength === 5
  if (hasZero && n === 0) return 0
  const offset = hasZero ? 1 : 0
  const mod100 = n % 100
  if (mod100 === 1) return offset
  if (mod100 === 2) return offset + 1
  if (mod100 === 3 || mod100 === 4) return offset + 2
  return offset + 3
}

export function createAppI18n(locale: AppLocale = DEFAULT_LOCALE) {
  return createI18n<[MessageSchema], AppLocale, false>({
    legacy: false,
    locale,
    fallbackLocale: 'en',
    messages: { sl, en },
    pluralRules: { sl: slovenianPlural },
    missingWarn: import.meta.env.DEV,
    fallbackWarn: false,
  })
}

export const i18n = createAppI18n()
