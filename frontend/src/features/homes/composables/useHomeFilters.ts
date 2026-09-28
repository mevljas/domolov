import { computed, type ComputedRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter, type LocationQuery, type LocationQueryRaw } from 'vue-router'
import {
  BOOKMARK_STAGES,
  HOME_SORTS,
  type BookmarkStage,
  type DismissedFilter,
  type HomeSort,
  type HomesQuery,
  type MarketStatus,
} from '@/api/types'
import { formatNumber, formatPrice } from '@/shared/lib/format'
import { type AppLocale, isAppLocale } from '@/shared/lib/locale'

const BOOL_KEYS = ['Bookmarked', 'Unseen', 'Reposted', 'HasDuplicates'] as const
const NUMBER_KEYS = [
  'MinPrice',
  'MaxPrice',
  'MinSize',
  'MaxSize',
  'MaxPricePerM2',
  'MinRooms',
] as const
const STRING_KEYS = ['WatchId', 'Q', 'PropertyType'] as const

const MARKET_STATUSES: readonly MarketStatus[] = ['onMarket', 'offMarket', 'all']
const DISMISSED_FILTERS: readonly DismissedFilter[] = ['exclude', 'include', 'only']

/** Query keys owned by the homes filter bar (Page/PageSize stay on the infinite query). */
export const HOME_FILTER_KEYS = [
  'WatchId',
  'Q',
  'Status',
  'Bookmarked',
  'Stage',
  'Unseen',
  'Dismissed',
  'Reposted',
  'HasDuplicates',
  'MinPrice',
  'MaxPrice',
  'MinSize',
  'MaxSize',
  'MaxPricePerM2',
  'PropertyType',
  'MinRooms',
  'Sort',
] as const satisfies readonly (keyof HomesQuery)[]

export type HomeFilterKey = (typeof HOME_FILTER_KEYS)[number]

export const HOME_FILTER_DEFAULTS: Pick<HomesQuery, 'Dismissed' | 'Sort' | 'Status'> = {
  Dismissed: 'exclude',
  Sort: 'newest',
  Status: 'onMarket',
}

export interface HomeFilterChip {
  id: string
  label: string
  keys: HomeFilterKey[]
}

function firstString(value: unknown): string | undefined {
  if (typeof value === 'string') return value
  if (Array.isArray(value) && typeof value[0] === 'string') return value[0]
  return undefined
}

function parseBool(raw: string | undefined): boolean | undefined {
  if (raw === 'true') return true
  if (raw === 'false') return false
  return undefined
}

function parseNumber(raw: string | undefined): number | undefined {
  if (raw === undefined || raw === '') return undefined
  const n = Number(raw)
  return Number.isFinite(n) ? n : undefined
}

function isMarketStatus(value: string): value is MarketStatus {
  return (MARKET_STATUSES as readonly string[]).includes(value)
}

function isDismissedFilter(value: string): value is DismissedFilter {
  return (DISMISSED_FILTERS as readonly string[]).includes(value)
}

function isHomeSort(value: string): value is HomeSort {
  return (HOME_SORTS as readonly string[]).includes(value)
}

function isBookmarkStage(value: string): value is BookmarkStage {
  return (BOOKMARK_STAGES as readonly string[]).includes(value)
}

/** Parse a route query object into a HomesQuery, applying defaults. */
export function parseHomesQuery(query: LocationQuery): HomesQuery {
  const result: HomesQuery = { ...HOME_FILTER_DEFAULTS }

  for (const key of STRING_KEYS) {
    const raw = firstString(query[key])?.trim()
    if (raw) result[key] = raw
  }

  for (const key of BOOL_KEYS) {
    const value = parseBool(firstString(query[key]))
    if (value !== undefined) result[key] = value
  }

  for (const key of NUMBER_KEYS) {
    const value = parseNumber(firstString(query[key]))
    if (value !== undefined) result[key] = value
  }

  const status = firstString(query.Status)
  if (status && isMarketStatus(status)) result.Status = status

  const dismissed = firstString(query.Dismissed)
  if (dismissed && isDismissedFilter(dismissed)) result.Dismissed = dismissed

  const sort = firstString(query.Sort)
  if (sort && isHomeSort(sort)) result.Sort = sort

  const stage = firstString(query.Stage)
  if (stage && isBookmarkStage(stage)) result.Stage = stage

  return result
}

function serializeValue(value: unknown): string | null {
  if (value === undefined || value === null || value === '') return null
  if (typeof value === 'boolean') return value ? 'true' : 'false'
  return String(value)
}

/** Build a query object from HomesQuery, omitting defaults and blank values. */
export function serializeHomesQuery(
  filters: HomesQuery,
  preserve: LocationQuery = {},
): LocationQueryRaw {
  const next: LocationQueryRaw = {}

  for (const [key, value] of Object.entries(preserve)) {
    if (!(HOME_FILTER_KEYS as readonly string[]).includes(key)) {
      next[key] = value as string | string[] | null
    }
  }

  for (const key of HOME_FILTER_KEYS) {
    const value = filters[key]
    if (value === undefined || value === null || value === '') continue
    if (
      key in HOME_FILTER_DEFAULTS &&
      value === HOME_FILTER_DEFAULTS[key as keyof typeof HOME_FILTER_DEFAULTS]
    ) {
      continue
    }
    const serialized = serializeValue(value)
    if (serialized !== null) next[key] = serialized
  }

  return next
}

function euro(value: number, locale: AppLocale): string {
  return formatPrice(value, locale)
}

function area(value: number, locale: AppLocale): string {
  return `${formatNumber(value, locale, 0)}\u00a0m²`
}

export function buildFilterChips(
  filters: HomesQuery,
  t: (key: string, values?: Record<string, unknown>) => string,
  locale: AppLocale,
  watchName?: string | null,
): HomeFilterChip[] {
  const chips: HomeFilterChip[] = []

  if (filters.Q) {
    chips.push({ id: 'Q', label: filters.Q, keys: ['Q'] })
  }
  if (filters.WatchId) {
    chips.push({
      id: 'WatchId',
      label: watchName ?? t('homes.filter.watch'),
      keys: ['WatchId'],
    })
  }
  if (filters.Status && filters.Status !== HOME_FILTER_DEFAULTS.Status) {
    chips.push({
      id: 'Status',
      label: t(`homes.filter.status_${filters.Status}`),
      keys: ['Status'],
    })
  }
  if (filters.Unseen) {
    chips.push({ id: 'Unseen', label: t('homes.filter.unseen'), keys: ['Unseen'] })
  }
  if (filters.Bookmarked) {
    chips.push({ id: 'Bookmarked', label: t('homes.filter.bookmarked'), keys: ['Bookmarked'] })
  }
  if (filters.Stage) {
    chips.push({
      id: 'Stage',
      label: t(`homes.bookmark.stages.${filters.Stage}`),
      keys: ['Stage'],
    })
  }
  if (filters.Reposted) {
    chips.push({ id: 'Reposted', label: t('homes.filter.reposted'), keys: ['Reposted'] })
  }
  if (filters.HasDuplicates) {
    chips.push({
      id: 'HasDuplicates',
      label: t('homes.filter.hasDuplicates'),
      keys: ['HasDuplicates'],
    })
  }
  if (filters.Dismissed && filters.Dismissed !== HOME_FILTER_DEFAULTS.Dismissed) {
    chips.push({
      id: 'Dismissed',
      label: t(`homes.filter.dismissed_${filters.Dismissed}`),
      keys: ['Dismissed'],
    })
  }
  if (filters.MinPrice !== undefined || filters.MaxPrice !== undefined) {
    const min = filters.MinPrice !== undefined ? euro(filters.MinPrice, locale) : '…'
    const max = filters.MaxPrice !== undefined ? euro(filters.MaxPrice, locale) : '…'
    chips.push({
      id: 'price',
      label: `${min} – ${max}`,
      keys: ['MinPrice', 'MaxPrice'],
    })
  }
  if (filters.MinSize !== undefined || filters.MaxSize !== undefined) {
    const min = filters.MinSize !== undefined ? area(filters.MinSize, locale) : '…'
    const max = filters.MaxSize !== undefined ? area(filters.MaxSize, locale) : '…'
    chips.push({
      id: 'size',
      label: `${min} – ${max}`,
      keys: ['MinSize', 'MaxSize'],
    })
  }
  if (filters.MaxPricePerM2 !== undefined) {
    chips.push({
      id: 'MaxPricePerM2',
      label: `≤ ${euro(filters.MaxPricePerM2, locale)}/m²`,
      keys: ['MaxPricePerM2'],
    })
  }
  if (filters.MinRooms !== undefined) {
    chips.push({
      id: 'MinRooms',
      label: t('homes.filter.minRoomsChip', { n: filters.MinRooms }),
      keys: ['MinRooms'],
    })
  }
  if (filters.PropertyType) {
    chips.push({
      id: 'PropertyType',
      label: filters.PropertyType,
      keys: ['PropertyType'],
    })
  }
  if (filters.Sort && filters.Sort !== HOME_FILTER_DEFAULTS.Sort) {
    chips.push({
      id: 'Sort',
      label: t(`homes.sorts.${filters.Sort}`),
      keys: ['Sort'],
    })
  }

  return chips
}

export function useHomeFilters(options?: {
  watchName?: ComputedRef<string | null | undefined> | (() => string | null | undefined)
}) {
  const route = useRoute()
  const router = useRouter()
  const { t, locale } = useI18n()

  const appLocale = computed<AppLocale>(() => (isAppLocale(locale.value) ? locale.value : 'sl'))

  const filters = computed(() => parseHomesQuery(route.query))

  const chips = computed(() => {
    const name =
      typeof options?.watchName === 'function' ? options.watchName() : options?.watchName?.value
    return buildFilterChips(filters.value, t, appLocale.value, name)
  })

  const hasActiveFilters = computed(() => chips.value.length > 0)

  async function replaceQuery(next: HomesQuery) {
    await router.replace({ query: serializeHomesQuery(next, route.query) })
  }

  async function setFilters(patch: Partial<HomesQuery>) {
    await replaceQuery({ ...filters.value, ...patch })
  }

  async function clearKeys(keys: HomeFilterKey[]) {
    const next: HomesQuery = { ...filters.value }
    for (const key of keys) {
      if (key in HOME_FILTER_DEFAULTS) {
        next[key] = HOME_FILTER_DEFAULTS[key as keyof typeof HOME_FILTER_DEFAULTS] as never
      } else {
        delete next[key]
      }
    }
    await replaceQuery(next)
  }

  async function clearChip(chip: HomeFilterChip) {
    await clearKeys(chip.keys)
  }

  async function resetFilters() {
    await replaceQuery({ ...HOME_FILTER_DEFAULTS })
  }

  return {
    filters,
    chips,
    hasActiveFilters,
    setFilters,
    clearKeys,
    clearChip,
    resetFilters,
  }
}
