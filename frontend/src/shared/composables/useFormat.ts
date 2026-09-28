import { computed, shallowRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useIntervalFn } from '@vueuse/core'
import {
  type DateInput,
  type DateStyle,
  formatArea,
  formatDate,
  formatDateTime,
  formatDuration,
  formatNumber,
  formatPercentChange,
  formatPrice,
  formatPriceChange,
  formatPricePerArea,
  formatRelativeTime,
  formatTime,
} from '@/shared/lib/format'
import { type AppLocale, isAppLocale } from '@/shared/lib/locale'

/** Formatters bound to the active UI locale. */
export function useFormat() {
  const { locale } = useI18n()
  const current = computed<AppLocale>(() => (isAppLocale(locale.value) ? locale.value : 'sl'))

  return {
    locale: current,
    price: (value: number | null | undefined, currency = 'EUR') => formatPrice(value, current.value, { currency }),
    priceChange: (delta: number | null | undefined, currency = 'EUR') =>
      formatPriceChange(delta, current.value, currency),
    percentChange: (from: number | null | undefined, to: number | null | undefined) =>
      formatPercentChange(from, to, current.value),
    number: (value: number | null | undefined, digits = 0) => formatNumber(value, current.value, digits),
    area: (m2: number | null | undefined) => formatArea(m2, current.value),
    pricePerArea: (price: number | null | undefined, m2: number | null | undefined) =>
      formatPricePerArea(price, m2, current.value),
    date: (value: DateInput, style: DateStyle = 'medium') => formatDate(value, current.value, style),
    time: (value: DateInput) => formatTime(value, current.value),
    dateTime: (value: DateInput, style: DateStyle = 'medium') => formatDateTime(value, current.value, style),
    relative: (value: DateInput, now?: DateInput) => formatRelativeTime(value, current.value, now),
    duration: (ms: number | null | undefined) => formatDuration(ms, current.value),
    bytes: (bytes: number | null | undefined) => formatBytes(bytes, current.value),
  }
}

/** "1,2 GB" style sizes using binary units. */
export function formatBytes(bytes: number | null | undefined, locale: AppLocale): string {
  if (bytes === null || bytes === undefined || !Number.isFinite(bytes) || bytes < 0) return '—'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${formatNumber(value, locale, unit === 0 ? 0 : 1)}\u00a0${units[unit]}`
}

/** A ticking clock for relative times and elapsed timers. VueUse 15 dropped the interval option on useNow. */
export function useTicker(intervalMs = 30_000) {
  const now = shallowRef(new Date())
  useIntervalFn(() => {
    now.value = new Date()
  }, intervalMs)
  return now
}
