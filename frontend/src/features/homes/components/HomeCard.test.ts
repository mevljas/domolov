import { describe, expect, it, vi, beforeEach } from 'vitest'
import { http, HttpResponse } from 'msw'
import type { HomeSummary } from '@/api/types'
import { server } from '@/test/msw'
import { flushPromises, renderWithPlugins } from '@/test/render'
import HomeCard from './HomeCard.vue'

const home: HomeSummary = {
  id: '11111111-1111-7111-8111-111111111111',
  title: 'Sunny loft in Ljubljana',
  imageUrl: null,
  location: 'Ljubljana',
  propertyType: 'Apartment',
  roomCount: 3,
  rooms: '3',
  sizeM2: 84,
  sizeText: '84 m²',
  landSizeM2: null,
  landSizeText: null,
  floorText: '2',
  yearBuilt: 2005,
  price: 210_000,
  previousPrice: 230_000,
  currency: 'EUR',
  priceChangedAt: '2026-09-01T10:00:00Z',
  pricePerM2: 2500,
  firstSeenAt: '2026-08-01T10:00:00Z',
  lastSeenAt: '2026-09-20T10:00:00Z',
  offMarketAt: null,
  isUnseen: true,
  isDismissed: false,
  listingCount: 2,
  activeListingCount: 1,
  repostCount: 1,
  primaryListingId: '22222222-2222-7222-8222-222222222222',
  url: 'https://www.nepremicnine.net/example',
  providerId: 'nepremicnine',
  bookmark: null,
}

const routes = [
  { path: '/', name: 'dashboard', component: { template: '<div />' } },
  { path: '/homes', name: 'homes', component: { template: '<div />' } },
  { path: '/homes/:id', name: 'home', component: { template: '<div />' }, props: true },
]

describe('HomeCard', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('renders price, unseen marker and repost badge', async () => {
    localStorage.setItem('domolov:preferences', JSON.stringify({ theme: 'system', locale: 'en' }))

    const { wrapper } = await renderWithPlugins(HomeCard, {
      props: { home },
      locale: 'en',
      routes,
      route: '/homes',
    })

    expect(wrapper.text()).toContain('€210,000')
    expect(wrapper.find('[aria-label="Unseen"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Repost')
    expect(wrapper.text()).toContain('2 ads')
    expect(wrapper.get('a').attributes('href')).toBe(`/homes/${home.id}`)
  })

  it('bookmarks via the mutation when the button is clicked', async () => {
    let bookmarked: unknown
    server.use(
      http.put('*/api/homes/:id/bookmark', async ({ request, params }) => {
        bookmarked = { id: params.id, body: await request.json() }
        return HttpResponse.json({
          stage: 'interested',
          note: null,
          createdAt: '2026-09-27T10:00:00Z',
          updatedAt: '2026-09-27T10:00:00Z',
        })
      }),
    )

    const { wrapper } = await renderWithPlugins(HomeCard, {
      props: { home },
      locale: 'en',
      routes,
      route: '/homes',
    })

    await wrapper.get('[data-testid="home-bookmark"]').trigger('click')
    await flushPromises()
    await vi.waitFor(() => {
      expect(bookmarked).toEqual({
        id: home.id,
        body: { stage: 'interested', note: null },
      })
    })
  })
})
