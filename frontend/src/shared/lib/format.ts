/**
 * Locale-aware formatting for prices, areas, dates and relative times.
 * Output uses Intl, so separators are locale-correct and the space before units is a
 * non-breaking space (U+00A0) — "210.000 €" never wraps between the number and the symbol.
 */
import { type AppLocale, intlLocale } from './locale'

export type DateInput = Date | string | number

const cache = new Map<string, Intl.NumberFormat | Intl.DateTimeFormat | Intl.RelativeTimeFormat>()

function cached<T extends Intl.NumberFormat | Intl.DateTimeFormat | Intl.RelativeTimeFormat>(
  key: string,
  create: () => T,
): T {
  let value = cache.get(key) as T | undefined
  if (!value) {
    value = create()
    cache.set(key, value)
  }
  return value
}

function numberFormat(locale: AppLocale, options: Intl.NumberFormatOptions): Intl.NumberFormat {
  return cached(
    `n|${locale}|${JSON.stringify(options)}`,
    () => new Intl.NumberFormat(intlLocale(locale), options),
  )
}

function dateFormat(locale: AppLocale, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  return cached(
    `d|${locale}|${JSON.stringify(options)}`,
    () => new Intl.DateTimeFormat(intlLocale(locale), options),
  )
}

export function toDate(value: DateInput): Date {
  return value instanceof Date ? value : new Date(value)
}

export interface PriceOptions {
  currency?: string
  /** Show a leading + / − sign, e.g. for price changes. */
  signed?: boolean
  /** Fraction digits; whole euros by default. */
  fractionDigits?: number
}

/** "210.000 €" (sl) / "€210,000" (en). Returns an em dash for missing values. */
export function formatPrice(
  amount: number | null | undefined,
  locale: AppLocale,
  { currency = 'EUR', signed = false, fractionDigits = 0 }: PriceOptions = {},
): string {
  if (amount === null || amount === undefined || !Number.isFinite(amount)) return '—'
  return numberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
    useGrouping: 'always',
    signDisplay: signed ? 'exceptZero' : 'auto',
  }).format(amount)
}

/** Signed price change, e.g. "−12.000 €" / "+€5,000". */
export function formatPriceChange(
  delta: number | null | undefined,
  locale: AppLocale,
  currency = 'EUR',
): string {
  return formatPrice(delta, locale, { currency, signed: true })
}

/** Relative change as a percentage, e.g. "−5,2 %" (sl) / "−5.2%" (en). */
export function formatPercentChange(
  from: number | null | undefined,
  to: number | null | undefined,
  locale: AppLocale,
): string {
  if (!from || to === null || to === undefined || !Number.isFinite(from) || !Number.isFinite(to)) {
    return '—'
  }
  return numberFormat(locale, {
    style: 'percent',
    maximumFractionDigits: 1,
    signDisplay: 'exceptZero',
  }).format((to - from) / from)
}

export function formatNumber(
  value: number | null | undefined,
  locale: AppLocale,
  fractionDigits = 0,
): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—'
  return numberFormat(locale, {
    maximumFractionDigits: fractionDigits,
    useGrouping: 'always',
  }).format(value)
}

/** "84,5 m²" (sl) / "84.5 m²" (en). */
export function formatArea(m2: number | null | undefined, locale: AppLocale): string {
  if (m2 === null || m2 === undefined || !Number.isFinite(m2)) return '—'
  return `${formatNumber(m2, locale, 1)}\u00a0m²`
}

/** "2.500 €/m²". */
export function formatPricePerArea(
  price: number | null | undefined,
  m2: number | null | undefined,
  locale: AppLocale,
  currency = 'EUR',
): string {
  if (!price || !m2 || !Number.isFinite(price) || !Number.isFinite(m2)) return '—'
  return `${formatPrice(price / m2, locale, { currency })}/m²`
}

export type DateStyle = 'short' | 'medium' | 'long'

/** "27. sep. 2026" (sl, medium) / "27 Sept 2026" (en, medium). */
export function formatDate(
  value: DateInput,
  locale: AppLocale,
  style: DateStyle = 'medium',
): string {
  return dateFormat(locale, { dateStyle: style }).format(toDate(value))
}

export function formatTime(value: DateInput, locale: AppLocale): string {
  return dateFormat(locale, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(
    toDate(value),
  )
}

export function formatDateTime(
  value: DateInput,
  locale: AppLocale,
  style: DateStyle = 'medium',
): string {
  return dateFormat(locale, { dateStyle: style, timeStyle: 'short', hourCycle: 'h23' }).format(
    toDate(value),
  )
}

const RELATIVE_STEPS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['second', 60],
  ['minute', 60],
  ['hour', 24],
  ['day', 7],
  ['week', 4.34524],
  ['month', 12],
  ['year', Number.POSITIVE_INFINITY],
]

/** "pred 3 urami" / "3 hours ago"; "čez 2 dni" / "in 2 days"; "zdaj" / "now". */
export function formatRelativeTime(
  value: DateInput,
  locale: AppLocale,
  now: DateInput = Date.now(),
): string {
  const rtf = cached(
    `r|${locale}`,
    () => new Intl.RelativeTimeFormat(intlLocale(locale), { numeric: 'auto', style: 'long' }),
  )
  let delta = (toDate(value).getTime() - toDate(now).getTime()) / 1000
  if (Math.abs(delta) < 45) return rtf.format(0, 'second')
  for (const [unit, size] of RELATIVE_STEPS) {
    if (Math.abs(delta) < size) return rtf.format(Math.round(delta), unit)
    delta /= size
  }
  return rtf.format(Math.round(delta), 'year')
}

/** Compact elapsed duration for scans: "850 ms", "42 s", "3 min 5 s", "2 h 4 min". */
export function formatDuration(ms: number | null | undefined, locale: AppLocale): string {
  if (ms === null || ms === undefined || !Number.isFinite(ms) || ms < 0) return '—'
  if (ms < 1000) return `${formatNumber(ms, locale)}\u00a0ms`
  const totalSeconds = Math.round(ms / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  if (hours > 0) return minutes > 0 ? `${hours}\u00a0h ${minutes}\u00a0min` : `${hours}\u00a0h`
  if (minutes > 0)
    return seconds > 0 ? `${minutes}\u00a0min ${seconds}\u00a0s` : `${minutes}\u00a0min`
  return `${seconds}\u00a0s`
}
