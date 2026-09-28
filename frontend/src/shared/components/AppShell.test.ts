import { describe, expect, it } from 'vitest'
import { h } from 'vue'
import { renderWithPlugins } from '@/test/render'
import { useNavBadges } from '@/shared/composables/useNavBadges'
import AppShell from './AppShell.vue'

async function renderShell(route = '/') {
  return renderWithPlugins(AppShell, {
    route,
    slots: { default: () => h('h1', 'Page title') },
  })
}

describe('AppShell', () => {
  it('renders the landmarks and a skip link to main content', async () => {
    const { wrapper } = await renderShell()

    expect(wrapper.find('header').exists()).toBe(true)
    expect(wrapper.find('aside').exists()).toBe(true)
    expect(wrapper.find('main#main').exists()).toBe(true)
    expect(wrapper.get('main#main').attributes('tabindex')).toBe('-1')

    const skip = wrapper.get('a.skip-link')
    expect(skip.attributes('href')).toBe('#main')
    expect(skip.text()).toBe('Skip to content')
    expect(wrapper.findAll('h1')).toHaveLength(1)
  })

  it('renders every destination in the labelled main navigation', async () => {
    const { wrapper } = await renderShell()
    const nav = wrapper.get('nav[aria-label="Main"]')
    const labels = nav.findAll('a').map((a) => a.text())

    expect(labels).toEqual([
      'Dashboard',
      'Homes',
      'Bookmarks',
      'Matches',
      'Watches',
      'Scans',
      'Settings',
    ])
    expect(nav.get('[data-testid="nav-homes"]').attributes('href')).toBe('/homes')
  })

  it('marks the current page and highlights the parent section on detail routes', async () => {
    const { wrapper, router } = await renderShell('/watches')
    expect(wrapper.get('[data-testid="nav-watches"]').attributes('aria-current')).toBe('page')

    await router.push('/watches/12')
    await wrapper.vm.$nextTick()
    expect(wrapper.get('[data-testid="nav-watches"]').attributes('aria-current')).toBe('true')
    expect(wrapper.get('[data-testid="nav-dashboard"]').attributes('aria-current')).toBeUndefined()
  })

  it('renders the mobile bottom navigation with four destinations and More', async () => {
    const { wrapper } = await renderShell()
    const bottom = wrapper.get('nav[aria-label="Primary"]')

    expect(bottom.findAll('a')).toHaveLength(4)
    expect(bottom.get('[data-testid="bottom-nav-more"]').element.tagName).toBe('BUTTON')
  })

  it('shows navigation badges such as unseen matches', async () => {
    useNavBadges().setBadge('matches', 3)
    const { wrapper } = await renderShell()

    expect(wrapper.get('[data-testid="nav-matches"]').text()).toContain('3')
    useNavBadges().setBadge('matches', 0)
  })

  it('emits signOut from the sidebar', async () => {
    const { wrapper } = await renderShell()
    await wrapper.get('[data-testid="sign-out"]').trigger('click')
    expect(wrapper.emitted('signOut')).toHaveLength(1)
  })

  it('collapses the sidebar and keeps labels for assistive tech', async () => {
    const { wrapper } = await renderShell()
    await wrapper.get('[data-testid="sidebar-toggle"]').trigger('click')

    expect(wrapper.get('[data-testid="sidebar"]').attributes('data-collapsed')).toBe('true')
    expect(wrapper.get('[data-testid="nav-homes"]').text()).toContain('Homes')
  })
})
