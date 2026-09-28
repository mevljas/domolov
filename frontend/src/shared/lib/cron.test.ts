import { describe, expect, it } from 'vitest'
import {
  ALL_DAYS,
  createCronModel,
  describeCron,
  EVERY_12_HOURS_CRON,
  EVERY_6_HOURS_CRON,
  EVERY_HOUR_CRON,
  formatTimeOfDay,
  parse,
  parseCron,
  toCron,
  validateCron,
  type WatchCronMode,
} from './cron'

// Mirrors tests/Domolov.UnitTests/WatchCronScheduleTests.cs case for case.
describe('WatchCronSchedule port (C# parity)', () => {
  it.each<[WatchCronMode, string]>([
    ['everyHour', EVERY_HOUR_CRON],
    ['every6Hours', EVERY_6_HOURS_CRON],
    ['every12Hours', EVERY_12_HOURS_CRON],
  ])('Interval_modes_round_trip: %s', (mode, cron) => {
    expect(toCron(createCronModel({ mode }))).toBe(cron)
    expect(parseCron(cron).mode).toBe(mode)
  })

  it('Weekly_weekdays_at_time_round_trips', () => {
    const model = createCronModel({
      mode: 'weekly',
      time: { hour: 9, minute: 30 },
      days: [1, 2, 3, 4, 5],
    })

    const cron = toCron(model)
    expect(cron).toBe('30 9 * * 1-5')

    const parsed = parseCron(cron)
    expect(parsed.mode).toBe('weekly')
    expect(parsed.time).toEqual({ hour: 9, minute: 30 })
    expect(parsed.days).toEqual(model.days)
  })

  it('Weekly_all_days_uses_star_day_field', () => {
    const model = createCronModel({
      mode: 'weekly',
      time: { hour: 8, minute: 0 },
      days: ALL_DAYS,
    })

    const cron = toCron(model)
    expect(cron).toBe('0 8 * * *')

    const parsed = parseCron(cron)
    expect(parsed.mode).toBe('weekly')
    expect(parsed.time).toEqual({ hour: 8, minute: 0 })
    expect(parsed.days).toHaveLength(7)
  })

  it('Weekly_noncontiguous_days_list', () => {
    const model = createCronModel({
      mode: 'weekly',
      time: { hour: 7, minute: 15 },
      days: [1, 3, 5],
    })

    const cron = toCron(model)
    expect(cron).toBe('15 7 * * 1,3,5')

    const parsed = parseCron(cron)
    expect(parsed.mode).toBe('weekly')
    expect(parsed.days).toEqual(model.days)
  })

  it('Unparseable_cron_is_custom', () => {
    const parsed = parseCron('15 2 1 * *')
    expect(parsed.mode).toBe('custom')
    expect(parsed.customCron).toBe('15 2 1 * *')
    expect(toCron(parsed)).toBe('15 2 1 * *')
  })

  it('Empty_cron_defaults_to_every_6_hours', () => {
    expect(parseCron('').mode).toBe('every6Hours')
    expect(parseCron(null).mode).toBe('every6Hours')
    expect(parseCron(undefined).mode).toBe('every6Hours')
  })

  it('TryValidate_accepts_known_expressions', () => {
    expect(validateCron('0 * * * *')).toEqual({ valid: true, error: null })
  })

  it('TryValidate_rejects_invalid_expressions', () => {
    const result = validateCron('not-a-cron')
    expect(result.valid).toBe(false)
    expect(result.error).toBeTruthy()
  })

  it('Sunday_as_7_normalizes_on_parse', () => {
    const parsed = parseCron('0 10 * * 7')
    expect(parsed.mode).toBe('weekly')
    expect(parsed.days).toEqual([0])
  })
})

describe('toCron edge cases', () => {
  it('sorts and de-duplicates weekly days and compresses mixed ranges', () => {
    expect(
      toCron(
        createCronModel({
          mode: 'weekly',
          time: { hour: 6, minute: 5 },
          days: [5, 1, 2, 3, 1, 0],
        }),
      ),
    ).toBe('5 6 * * 0-3,5')
  })

  it('single weekly day', () => {
    expect(toCron(createCronModel({ mode: 'weekly', days: [6] }))).toBe('0 9 * * 6')
  })

  it('empty weekly day list falls back to every day', () => {
    expect(toCron(createCronModel({ mode: 'weekly', days: [] }))).toBe('0 9 * * *')
  })

  it('custom trims and falls back to every 6 hours when blank', () => {
    expect(toCron(createCronModel({ mode: 'custom', customCron: '  5 4 * * 1  ' }))).toBe(
      '5 4 * * 1',
    )
    expect(toCron(createCronModel({ mode: 'custom', customCron: '   ' }))).toBe(EVERY_6_HOURS_CRON)
    expect(toCron(createCronModel({ mode: 'custom', customCron: null }))).toBe(EVERY_6_HOURS_CRON)
  })

  it('unknown modes fall back to every 6 hours', () => {
    expect(toCron({ ...createCronModel(), mode: 'bogus' as WatchCronMode })).toBe(
      EVERY_6_HOURS_CRON,
    )
  })
})

describe('parseCron edge cases', () => {
  it('trims surrounding whitespace before matching presets', () => {
    expect(parse('  0 * * * *  ').mode).toBe('everyHour')
  })

  it.each([
    ['0 */2 * * *', 'interval hour'],
    ['*/5 9 * * *', 'interval minute'],
    ['0 9 1 * *', 'day-of-month set'],
    ['0 9 * 1 *', 'month set'],
    ['60 9 * * *', 'minute out of range'],
    ['0 24 * * *', 'hour out of range'],
    ['0 9 * * 8', 'day out of range'],
    ['0 9 * * 5-1', 'descending range'],
    ['0 9 * * 1-2-3', 'malformed range'],
    ['0 9 * * MON', 'named day'],
    ['0 9 * * 1-x', 'non-numeric range end'],
    ['0 9 * *', 'four fields'],
  ])('%s is custom (%s)', (cron) => {
    const parsed = parseCron(cron)
    expect(parsed.mode).toBe('custom')
    expect(parsed.customCron).toBe(cron)
  })

  it('normalizes duplicate Sundays and ranges ending on 7 like C# does', () => {
    expect(parseCron('0 9 * * 0,7').days).toEqual([0])
    expect(parseCron('0 9 * * 6,7').days).toEqual([0, 6])
    // 7 becomes 0, so 5-7 is a descending range and falls back to custom (C# parity).
    expect(parseCron('0 9 * * 5-7').mode).toBe('custom')
  })
})

describe('validateCron', () => {
  it.each([
    '*/15 * * * *',
    '0 9 * * 1-5',
    '0 9 ? * MON-FRI',
    '0 0 1,15 * *',
    '30 6 L * *',
    '0 12 15W * *',
    '0 8 * JAN,JUL *',
    '0 8 * * 5L',
    '0 8 * * MON#2',
    '0 0-23/2 * * *',
  ])('accepts %s', (cron) => {
    expect(validateCron(cron).valid).toBe(true)
  })

  it.each([
    ['', /5 fields/],
    ['* * * *', /5 fields/],
    ['60 * * * *', /minute/],
    ['0 25 * * *', /hour/],
    ['0 0 0 * *', /day-of-month/],
    ['0 0 * 13 *', /month/],
    ['0 0 * * 8', /day-of-week/],
    ['0 0 * * 5-1', /day-of-week/],
    ['*/0 * * * *', /minute/],
    ['1/2/3 * * * *', /minute/],
    ['1,,2 * * * *', /minute/],
    ['0 1-2-3 * * *', /hour/],
  ])('rejects %j', (cron, message) => {
    const result = validateCron(cron)
    expect(result.valid).toBe(false)
    expect(result.error).toMatch(message)
  })
})

describe('describeCron', () => {
  it.each([
    [EVERY_HOUR_CRON, 'Every hour', 'Vsako uro'],
    [EVERY_6_HOURS_CRON, 'Every 6 hours', 'Vsakih 6 ur'],
    [EVERY_12_HOURS_CRON, 'Every 12 hours', 'Vsakih 12 ur'],
    ['0 8 * * *', 'Every day at 08:00', 'Vsak dan ob 08:00'],
    ['30 9 * * 1-5', 'Weekdays at 09:30', 'Ob delavnikih ob 09:30'],
    ['0 10 * * 0,6', 'Weekends at 10:00', 'Ob vikendih ob 10:00'],
    ['15 7 * * 1,3,5', 'Mon, Wed and Fri at 07:15', 'Pon, sre in pet ob 07:15'],
    ['0 18 * * 0,2', 'Tue and Sun at 18:00', 'Tor in ned ob 18:00'],
    ['15 2 1 * *', 'Custom cron: 15 2 1 * *', 'Cron po meri: 15 2 1 * *'],
    ['nope', 'Invalid cron: nope', 'Neveljaven cron: nope'],
  ])('%s', (cron, en, sl) => {
    expect(describeCron(cron, 'en')).toBe(en)
    expect(describeCron(cron, 'sl')).toBe(sl)
  })

  it('describes empty input as the default schedule', () => {
    expect(describeCron(null, 'en')).toBe('Every 6 hours')
  })

  it('formats time of day with two-digit 24h clock', () => {
    expect(formatTimeOfDay({ hour: 7, minute: 5 }, 'sl')).toBe('07:05')
    expect(formatTimeOfDay({ hour: 19, minute: 45 }, 'en')).toBe('19:45')
  })
})
