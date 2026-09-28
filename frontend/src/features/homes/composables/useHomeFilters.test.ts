import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, renderWithPlugins } from '@/test/render'
import {
  HOME_FILTER_DEFAULTS,
  parseHomesQuery,
  serializeHomesQuery,
  useHomeFilters,
} from './useHomeFilters'

type HomeFiltersApi = ReturnType<typeof useHomeFilters>
const probeState: { api: HomeFiltersApi | null } = { api: null }

const FilterProbe = defineComponent({
  name: 'FilterProbe',
  setup() {
    probeState.api = useHomeFilters()
    return () => h('div')
  },
})

function requireApi(): HomeFiltersApi {
  if (!probeState.api) throw new Error('FilterProbe did not initialize useHomeFilters')
  return probeState.api
}

describe('parseHomesQuery / serializeHomesQuery', () => {
  it('applies defaults when the query is empty', () => {
    expect(parseHomesQuery({})).toEqual(HOME_FILTER_DEFAULTS)
  })

  it('parses PascalCase filter keys and booleans', () => {
    expect(
      parseHomesQuery({
        Q: 'ljubljana',
        Status: 'offMarket',
        Unseen: 'true',
        Bookmarked: 'false',
        MinPrice: '100000',
        Sort: 'priceAsc',
        Dismissed: 'only',
        Stage: 'contacted',
      }),
    ).toEqual({
      ...HOME_FILTER_DEFAULTS,
      Q: 'ljubljana',
      Status: 'offMarket',
      Unseen: true,
      Bookmarked: false,
      MinPrice: 100_000,
      Sort: 'priceAsc',
      Dismissed: 'only',
      Stage: 'contacted',
    })
  })

  it('omits defaults when serializing and keeps unknown keys', () => {
    const serialized = serializeHomesQuery(
      {
        ...HOME_FILTER_DEFAULTS,
        Q: 'center',
        Unseen: true,
        MinPrice: 150_000,
      },
      { foo: 'bar', Status: 'onMarket' },
    )

    expect(serialized).toEqual({
      foo: 'bar',
      Q: 'center',
      Unseen: 'true',
      MinPrice: '150000',
    })
  })

  it('round-trips through serialize then parse', () => {
    const original = parseHomesQuery({
      Q: 'maribor',
      Status: 'all',
      Reposted: 'true',
      MaxSize: '120',
      Sort: 'size',
    })
    const query = serializeHomesQuery(original)
    expect(parseHomesQuery(query as Record<string, string>)).toEqual(original)
  })
})

describe('useHomeFilters', () => {
  it('reads filters from the route and writes updates back', async () => {
    probeState.api = null
    const { router } = await renderWithPlugins(FilterProbe, {
      route: '/homes?Q=ljubljana&Unseen=true&Sort=priceDrop',
    })
    const api = requireApi()

    expect(api.filters.value).toMatchObject({
      Q: 'ljubljana',
      Unseen: true,
      Sort: 'priceDrop',
      Status: 'onMarket',
      Dismissed: 'exclude',
    })
    expect(api.chips.value.map((c) => c.id)).toEqual(
      expect.arrayContaining(['Q', 'Unseen', 'Sort']),
    )

    await api.setFilters({ ...api.filters.value, Bookmarked: true, Q: 'celje' })
    await flushPromises()

    expect(router.currentRoute.value.query).toMatchObject({
      Q: 'celje',
      Unseen: 'true',
      Bookmarked: 'true',
      Sort: 'priceDrop',
    })
    expect(router.currentRoute.value.query.Status).toBeUndefined()
    expect(router.currentRoute.value.query.Dismissed).toBeUndefined()

    await api.resetFilters()
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({})
    expect(api.filters.value).toEqual(HOME_FILTER_DEFAULTS)
    expect(api.chips.value).toEqual([])
  })

  it('preserves unknown query keys when updating filters', async () => {
    probeState.api = null
    const { router } = await renderWithPlugins(FilterProbe, {
      route: '/homes?from=dashboard&Q=test',
    })
    const api = requireApi()

    await api.clearKeys(['Q'])
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ from: 'dashboard' })
  })
})
