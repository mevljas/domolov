import { describe, expect, it } from 'vitest'
import {
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
  toDate,
} from './format'

/** Intl uses NBSP / narrow NBSP around units; normalise for readable expectations. */
const plain = (value: string) => value.replace(/[\u00a0\u202f]/g, ' ')

describe('formatPrice', () => {
  it('formats euros the Slovenian way', () => {
    expect(plain(formatPrice(210000, 'sl'))).toBe('210.000 €')
    expect(plain(formatPrice(1500, 'sl'))).toBe('1.500 €')
  })

  it('formats euros for English readers', () => {
    expect(formatPrice(210000, 'en')).toBe('€210,000')
  })

  it('keeps the number and symbol together with a non-breaking space', () => {
    expect(formatPrice(99000, 'sl')).toContain('\u00a0€')
  })

  it('supports fraction digits, other currencies and signs', () => {
    expect(plain(formatPrice(1234.5, 'sl', { fractionDigits: 2 }))).toBe('1.234,50 €')
    expect(formatPrice(1000, 'en', { currency: 'USD' })).toBe('US$1,000')
    expect(plain(formatPrice(5000, 'sl', { signed: true }))).toBe('+5.000 €')
  })

  it('returns an em dash for missing or invalid values', () => {
    expect(formatPrice(null, 'sl')).toBe('—')
    expect(formatPrice(undefined, 'en')).toBe('—')
    expect(formatPrice(Number.NaN, 'en')).toBe('—')
  })
})

describe('price changes', () => {
  it('signs drops and rises (Slovenian uses a true minus sign)', () => {
    expect(plain(formatPriceChange(-12000, 'sl'))).toBe('\u221212.000 €')
    expect(formatPriceChange(-12000, 'en')).toBe('-€12,000')
    expect(formatPriceChange(5000, 'en')).toBe('+€5,000')
    expect(plain(formatPriceChange(0, 'sl'))).toBe('0 €')
  })

  it('computes percentage change', () => {
    expect(plain(formatPercentChange(200000, 190000, 'sl'))).toBe('\u22125 %')
    expect(formatPercentChange(200000, 210500, 'en')).toBe('+5.3%')
    expect(formatPercentChange(0, 100, 'en')).toBe('—')
    expect(formatPercentChange(100, null, 'en')).toBe('—')
  })
})

describe('numbers and areas', () => {
  it('formats plain numbers with grouping', () => {
    expect(formatNumber(1234567, 'sl')).toBe('1.234.567')
    expect(formatNumber(1234.56, 'en', 1)).toBe('1,234.6')
    expect(formatNumber(null, 'en')).toBe('—')
  })

  it('formats square metres', () => {
    expect(plain(formatArea(84.5, 'sl'))).toBe('84,5 m²')
    expect(plain(formatArea(120, 'en'))).toBe('120 m²')
    expect(formatArea(undefined, 'sl')).toBe('—')
  })

  it('formats price per square metre', () => {
    expect(plain(formatPricePerArea(210000, 84, 'sl'))).toBe('2.500 €/m²')
    expect(formatPricePerArea(210000, 0, 'sl')).toBe('—')
    expect(formatPricePerArea(null, 50, 'en')).toBe('—')
  })
})

describe('dates and times', () => {
  const date = new Date(2026, 8, 27, 14, 5)

  it('formats dates per locale', () => {
    expect(plain(formatDate(date, 'sl'))).toBe('27. sep. 2026')
    expect(formatDate(date, 'en', 'long')).toBe('27 September 2026')
    expect(formatDate(date.toISOString(), 'en', 'short')).toBe('27/09/2026')
  })

  it('formats times on a 24h clock', () => {
    expect(formatTime(date, 'sl')).toBe('14:05')
    expect(formatTime(date.getTime(), 'en')).toBe('14:05')
  })

  it('formats date and time together', () => {
    expect(plain(formatDateTime(date, 'sl'))).toMatch(/27\. sep\. 2026.*14:05/)
    expect(formatDateTime(date, 'en')).toMatch(/27 Sept 2026.*14:05/)
  })

  it('accepts Date, ISO strings and epoch numbers', () => {
    expect(toDate(date)).toBe(date)
    expect(toDate(date.toISOString()).getTime()).toBe(date.getTime())
    expect(toDate(date.getTime()).getTime()).toBe(date.getTime())
  })
})

describe('formatRelativeTime', () => {
  const now = new Date('2026-09-27T12:00:00Z')
  const ago = (seconds: number) => new Date(now.getTime() - seconds * 1000)

  it('says "now" for the last few seconds', () => {
    expect(formatRelativeTime(ago(10), 'en', now)).toBe('now')
    expect(formatRelativeTime(ago(10), 'sl', now)).toBe('zdaj')
  })

  it('uses hours with correct Slovenian grammar', () => {
    expect(formatRelativeTime(ago(3 * 3600), 'sl', now)).toBe('pred 3 urami')
    expect(formatRelativeTime(ago(3 * 3600), 'en', now)).toBe('3 hours ago')
  })

  it('picks sensible units across ranges', () => {
    expect(formatRelativeTime(ago(5 * 60), 'en', now)).toBe('5 minutes ago')
    expect(formatRelativeTime(ago(26 * 3600), 'en', now)).toBe('yesterday')
    expect(formatRelativeTime(ago(3 * 86400), 'en', now)).toBe('3 days ago')
    expect(formatRelativeTime(ago(14 * 86400), 'en', now)).toBe('2 weeks ago')
    expect(formatRelativeTime(ago(70 * 86400), 'en', now)).toBe('2 months ago')
    expect(formatRelativeTime(ago(800 * 86400), 'en', now)).toBe('2 years ago')
  })

  it('handles future times', () => {
    expect(formatRelativeTime(new Date(now.getTime() + 2 * 86400 * 1000), 'sl', now)).toBe(
      'pojutrišnjem',
    )
    expect(formatRelativeTime(new Date(now.getTime() + 3 * 86400 * 1000), 'sl', now)).toBe(
      'čez 3 dni',
    )
    expect(formatRelativeTime(new Date(now.getTime() + 2 * 3600 * 1000), 'en', now)).toBe(
      'in 2 hours',
    )
  })
})

describe('formatDuration', () => {
  it.each([
    [850, '850 ms'],
    [42_000, '42 s'],
    [185_000, '3 min 5 s'],
    [180_000, '3 min'],
    [7_440_000, '2 h 4 min'],
    [7_200_000, '2 h'],
  ])('%d ms → %s', (ms, expected) => {
    expect(plain(formatDuration(ms, 'sl'))).toBe(expected)
  })

  it('returns an em dash for missing or negative durations', () => {
    expect(formatDuration(null, 'en')).toBe('—')
    expect(formatDuration(-5, 'en')).toBe('—')
  })
})
