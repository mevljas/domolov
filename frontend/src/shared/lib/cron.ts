/**
 * Guided Watch schedules backed by a 5-field cron expression.
 * Faithful port of Domolov.Domain.Services.WatchCronSchedule; keep the two in sync.
 */
import { type AppLocale, intlLocale } from './locale'

export type WatchCronMode = 'everyHour' | 'every6Hours' | 'every12Hours' | 'weekly' | 'custom'

export const WATCH_CRON_MODES: readonly WatchCronMode[] = [
  'everyHour',
  'every6Hours',
  'every12Hours',
  'weekly',
  'custom',
]

/** Day of week as in Cronos / .NET: Sunday = 0 … Saturday = 6. */
export type DayOfWeek = 0 | 1 | 2 | 3 | 4 | 5 | 6

export interface TimeOfDay {
  hour: number
  minute: number
}

export interface WatchCronModel {
  mode: WatchCronMode
  /** Local time-of-day for `weekly`. */
  time: TimeOfDay
  /** Selected days for `weekly` (Sunday = 0). */
  days: readonly DayOfWeek[]
  /** Raw cron when mode is `custom`. */
  customCron?: string | null
}

export const EVERY_HOUR_CRON = '0 * * * *'
export const EVERY_6_HOURS_CRON = '0 */6 * * *'
export const EVERY_12_HOURS_CRON = '0 */12 * * *'

export const ALL_DAYS: readonly DayOfWeek[] = [0, 1, 2, 3, 4, 5, 6]

const DEFAULT_TIME: TimeOfDay = { hour: 9, minute: 0 }

export function createCronModel(overrides: Partial<WatchCronModel> = {}): WatchCronModel {
  return {
    mode: 'every6Hours',
    time: { ...DEFAULT_TIME },
    days: [...ALL_DAYS],
    customCron: null,
    ...overrides,
  }
}

export function toCron(model: WatchCronModel): string {
  switch (model.mode) {
    case 'everyHour':
      return EVERY_HOUR_CRON
    case 'every6Hours':
      return EVERY_6_HOURS_CRON
    case 'every12Hours':
      return EVERY_12_HOURS_CRON
    case 'weekly':
      return composeWeekly(model.time, model.days)
    case 'custom':
      return isBlank(model.customCron) ? EVERY_6_HOURS_CRON : model.customCron!.trim()
    default:
      return EVERY_6_HOURS_CRON
  }
}

export function parseCron(cron: string | null | undefined): WatchCronModel {
  const trimmed = isBlank(cron) ? EVERY_6_HOURS_CRON : cron!.trim()

  if (trimmed === EVERY_HOUR_CRON) return createCronModel({ mode: 'everyHour' })
  if (trimmed === EVERY_6_HOURS_CRON) return createCronModel({ mode: 'every6Hours' })
  if (trimmed === EVERY_12_HOURS_CRON) return createCronModel({ mode: 'every12Hours' })

  const weekly = tryParseWeekly(trimmed)
  if (weekly) return createCronModel({ mode: 'weekly', time: weekly.time, days: weekly.days })

  return createCronModel({ mode: 'custom', customCron: trimmed })
}

export type CronValidation = { valid: true; error: null } | { valid: false; error: string }

/**
 * Client-side validation of a standard 5-field cron (minute hour day-of-month month day-of-week),
 * accepting the syntax Cronos accepts for these fields: *, ?, lists, ranges, steps, month/day names,
 * and L / W / # in the day fields. The server remains the source of truth.
 */
export function validateCron(cron: string): CronValidation {
  const parts = cron.trim().split(/\s+/).filter(Boolean)
  if (parts.length !== 5) {
    return { valid: false, error: `Expected 5 fields but found ${parts.length}.` }
  }
  const [minute, hour, dom, month, dow] = parts as [string, string, string, string, string]
  const checks: [string, string, FieldSpec][] = [
    ['minute', minute, { min: 0, max: 59 }],
    ['hour', hour, { min: 0, max: 23 }],
    [
      'day-of-month',
      dom,
      { min: 1, max: 31, allowQuestion: true, special: /^(L(-\d{1,2})?|LW|\d{1,2}W)$/ },
    ],
    ['month', month, { min: 1, max: 12, names: MONTH_NAMES }],
    [
      'day-of-week',
      dow,
      {
        min: 0,
        max: 7,
        allowQuestion: true,
        names: DAY_NAMES,
        special: /^([0-7]|[A-Z]{3})(L|#[1-5])$/,
      },
    ],
  ]
  for (const [name, value, spec] of checks) {
    if (!isValidField(value.toUpperCase(), spec)) {
      return { valid: false, error: `Invalid ${name} field: '${value}'.` }
    }
  }
  return { valid: true, error: null }
}

/** Human summary such as "Weekdays at 09:30" / "Ob delavnikih ob 09:30". */
export function describeCron(cron: string | null | undefined, locale: AppLocale): string {
  const model = parseCron(cron)
  const words = PHRASES[locale]
  switch (model.mode) {
    case 'everyHour':
      return words.everyHour
    case 'every6Hours':
      return words.every6Hours
    case 'every12Hours':
      return words.every12Hours
    case 'weekly':
      return describeWeekly(model, locale)
    case 'custom': {
      const raw = model.customCron ?? ''
      return validateCron(raw).valid ? words.custom(raw) : words.invalid(raw)
    }
  }
}

export { parseCron as parse, describeCron as describe, validateCron as validate }

export function formatTimeOfDay(time: TimeOfDay, locale: AppLocale): string {
  const date = new Date(2000, 0, 1, time.hour, time.minute)
  return new Intl.DateTimeFormat(intlLocale(locale), {
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).format(date)
}

// ---- weekly helpers (mirrors the C# private helpers) ----------------------------------------

function composeWeekly(time: TimeOfDay, days: readonly DayOfWeek[]): string {
  let selected = sortedDistinct(days.length > 0 ? days : ALL_DAYS)
  if (selected.length === 0) selected = [...ALL_DAYS]
  const dayField = selected.length === 7 ? '*' : compressDayField(selected)
  return `${time.minute} ${time.hour} * * ${dayField}`
}

function tryParseWeekly(cron: string): { time: TimeOfDay; days: DayOfWeek[] } | null {
  const parts = cron
    .split(' ')
    .map((p) => p.trim())
    .filter(Boolean)
  if (parts.length !== 5) return null
  const [minuteField, hourField, dom, month, dow] = parts as [
    string,
    string,
    string,
    string,
    string,
  ]
  if (dom !== '*' || month !== '*') return null

  const minute = parseIntStrict(minuteField)
  if (minute === null || minute < 0 || minute > 59) return null
  const hour = parseIntStrict(hourField)
  if (hour === null || hour < 0 || hour > 23) return null

  if (dow === '*') return { time: { hour, minute }, days: [...ALL_DAYS] }

  const expanded = tryExpandDayField(dow)
  if (!expanded || expanded.length === 0) return null
  return { time: { hour, minute }, days: expanded }
}

function compressDayField(days: readonly DayOfWeek[]): string {
  if (days.length === 1) return String(days[0])
  const ranges: string[] = []
  let start = days[0]!
  let prev = days[0]!
  for (let i = 1; i < days.length; i++) {
    const day = days[i]!
    if (day === prev + 1) {
      prev = day
      continue
    }
    ranges.push(start === prev ? String(start) : `${start}-${prev}`)
    start = prev = day
  }
  ranges.push(start === prev ? String(start) : `${start}-${prev}`)
  return ranges.join(',')
}

function tryExpandDayField(field: string): DayOfWeek[] | null {
  const days: number[] = []
  for (const token of field
    .split(',')
    .map((t) => t.trim())
    .filter(Boolean)) {
    if (token.includes('-')) {
      const ends = token.split('-').map((e) => e.trim())
      if (ends.length !== 2) return null
      const rawFrom = parseIntStrict(ends[0]!)
      const rawTo = parseIntStrict(ends[1]!)
      if (rawFrom === null || rawTo === null) return null
      // Cronos allows 0-7 with 7 = Sunday.
      const from = normalizeDow(rawFrom)
      const to = normalizeDow(rawTo)
      if (from < 0 || to < 0 || from > to) return null
      for (let d = from; d <= to; d++) days.push(d)
    } else {
      const raw = parseIntStrict(token)
      if (raw === null) return null
      const day = normalizeDow(raw)
      if (day < 0) return null
      days.push(day)
    }
  }
  const result = sortedDistinct(days as DayOfWeek[])
  return result.length > 0 ? result : null
}

function normalizeDow(day: number): number {
  if (day === 7) return 0
  return day >= 0 && day <= 6 ? day : -1
}

function sortedDistinct(days: readonly DayOfWeek[]): DayOfWeek[] {
  return [...new Set(days)].sort((a, b) => a - b)
}

/** Mirrors int.TryParse for the ASCII digits the cron fields contain. */
function parseIntStrict(value: string): number | null {
  return /^[+-]?\d+$/.test(value) ? Number.parseInt(value, 10) : null
}

function isBlank(value: string | null | undefined): boolean {
  return value === null || value === undefined || value.trim() === ''
}

// ---- validation helpers ---------------------------------------------------------------------

interface FieldSpec {
  min: number
  max: number
  names?: readonly string[]
  allowQuestion?: boolean
  special?: RegExp
}

const MONTH_NAMES = [
  'JAN',
  'FEB',
  'MAR',
  'APR',
  'MAY',
  'JUN',
  'JUL',
  'AUG',
  'SEP',
  'OCT',
  'NOV',
  'DEC',
]
const DAY_NAMES = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT']

function isValidField(field: string, spec: FieldSpec): boolean {
  if (spec.allowQuestion && field === '?') return true
  return field.split(',').every((item) => item !== '' && isValidItem(item, spec))
}

function isValidItem(item: string, spec: FieldSpec): boolean {
  if (spec.special?.test(item)) return true
  const [range, step, ...rest] = item.split('/')
  if (rest.length > 0 || range === undefined) return false
  if (step !== undefined) {
    const stepValue = parseIntStrict(step)
    if (stepValue === null || stepValue < 1 || stepValue > spec.max) return false
  }
  if (range === '*') return true
  const bounds = range.split('-')
  if (bounds.length > 2) return false
  const values = bounds.map((b) => fieldValue(b, spec))
  if (values.some((v) => v === null)) return false
  if (values.length === 2 && (values[0] as number) > (values[1] as number)) {
    // Cronos accepts wrap-around ranges only for day-of-week names; keep it simple and strict.
    return false
  }
  return true
}

function fieldValue(token: string, spec: FieldSpec): number | null {
  const named = spec.names?.indexOf(token) ?? -1
  if (named >= 0) return spec.names === MONTH_NAMES ? named + 1 : named
  const value = parseIntStrict(token)
  if (value === null || value < spec.min || value > spec.max) return null
  return value
}

// ---- description ----------------------------------------------------------------------------

interface Phrases {
  everyHour: string
  every6Hours: string
  every12Hours: string
  everyDay: (time: string) => string
  weekdays: (time: string) => string
  weekends: (time: string) => string
  days: (days: string, time: string) => string
  custom: (cron: string) => string
  invalid: (cron: string) => string
  dayNames: readonly string[]
}

const PHRASES: Record<AppLocale, Phrases> = {
  en: {
    everyHour: 'Every hour',
    every6Hours: 'Every 6 hours',
    every12Hours: 'Every 12 hours',
    everyDay: (t) => `Every day at ${t}`,
    weekdays: (t) => `Weekdays at ${t}`,
    weekends: (t) => `Weekends at ${t}`,
    days: (d, t) => `${d} at ${t}`,
    custom: (c) => `Custom cron: ${c}`,
    invalid: (c) => `Invalid cron: ${c}`,
    dayNames: ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
  },
  sl: {
    everyHour: 'Vsako uro',
    every6Hours: 'Vsakih 6 ur',
    every12Hours: 'Vsakih 12 ur',
    everyDay: (t) => `Vsak dan ob ${t}`,
    weekdays: (t) => `Ob delavnikih ob ${t}`,
    weekends: (t) => `Ob vikendih ob ${t}`,
    days: (d, t) => `${capitalize(d)} ob ${t}`,
    custom: (c) => `Cron po meri: ${c}`,
    invalid: (c) => `Neveljaven cron: ${c}`,
    dayNames: ['ned', 'pon', 'tor', 'sre', 'čet', 'pet', 'sob'],
  },
}

function describeWeekly(model: WatchCronModel, locale: AppLocale): string {
  const words = PHRASES[locale]
  const time = formatTimeOfDay(model.time, locale)
  const days = sortedDistinct(model.days)
  const key = days.join(',')
  if (days.length === 7) return words.everyDay(time)
  if (key === '1,2,3,4,5') return words.weekdays(time)
  if (key === '0,6') return words.weekends(time)
  // Monday-first reading order for lists.
  const ordered = [...days.filter((d) => d !== 0), ...days.filter((d) => d === 0)]
  const list = new Intl.ListFormat(intlLocale(locale), {
    style: 'long',
    type: 'conjunction',
  }).format(ordered.map((d) => words.dayNames[d]!))
  return words.days(list, time)
}

function capitalize(value: string): string {
  return value.charAt(0).toLocaleUpperCase('sl-SI') + value.slice(1)
}
