import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import { setMediaQuery } from '@/test/media'
import { withSetup } from '@/test/with-setup'
import { useAnnouncer } from './useAnnouncer'
import { isApplePlatform, isCommandPaletteShortcut, useCommandPalette } from './useCommandPalette'
import { useLocale, useLocaleEffect } from './useLocale'
import { formatBadge, useNavBadges } from './useNavBadges'
import { useOnline } from './useOnline'
import { prefersReducedMotion, useReducedMotion } from './useReducedMotion'
import { useSidebar } from './useSidebar'
import {
  installRouteViewTransitions,
  supportsViewTransitions,
  useViewTransition,
} from './useViewTransition'

const REDUCE = '(prefers-reduced-motion: reduce)'

describe('useReducedMotion', () => {
  it('reflects the OS setting', async () => {
    const { result } = withSetup(useReducedMotion)
    expect(result.value).toBe(false)
    expect(prefersReducedMotion()).toBe(false)
    setMediaQuery(REDUCE, true)
    await nextTick()
    expect(result.value).toBe(true)
    expect(prefersReducedMotion()).toBe(true)
  })
})

describe('useViewTransition', () => {
  afterEach(() => {
    // @ts-expect-error test cleanup of an optional DOM API
    delete document.startViewTransition
  })

  function stubStartViewTransition() {
    const start = vi.fn((update: () => void | Promise<void>) => {
      const updateCallbackDone = Promise.resolve().then(update)
      return { updateCallbackDone, ready: updateCallbackDone, finished: updateCallbackDone }
    })
    Object.defineProperty(document, 'startViewTransition', {
      value: start,
      configurable: true,
      writable: true,
    })
    return start
  }

  it('runs updates directly when the API is missing', async () => {
    expect(supportsViewTransitions()).toBe(false)
    const { result } = withSetup(useViewTransition)
    const update = vi.fn()
    await result.run(update)
    expect(result.isEnabled.value).toBe(false)
    expect(update).toHaveBeenCalledOnce()
  })

  it('wraps updates in a view transition when supported and motion is allowed', async () => {
    const start = stubStartViewTransition()
    const { result } = withSetup(useViewTransition)
    const update = vi.fn()
    await result.run(update)
    expect(start).toHaveBeenCalledOnce()
    expect(update).toHaveBeenCalledOnce()
  })

  it('skips the transition under prefers-reduced-motion', async () => {
    const start = stubStartViewTransition()
    setMediaQuery(REDUCE, true)
    const { result } = withSetup(useViewTransition)
    await result.run(vi.fn())
    expect(start).not.toHaveBeenCalled()
  })

  it('wraps router navigations and settles after render', async () => {
    const start = stubStartViewTransition()
    const component = { render: () => null }
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/', component },
        { path: '/homes', component },
      ],
    })
    const uninstall = installRouteViewTransitions(router)
    await router.push('/')
    expect(start).not.toHaveBeenCalled()

    await router.push('/homes')
    expect(start).toHaveBeenCalledOnce()
    await router.push('/homes')
    expect(start).toHaveBeenCalledOnce()
    uninstall()
  })

  it('leaves navigation alone when disabled', async () => {
    const start = stubStartViewTransition()
    const component = { render: () => null }
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/', component },
        { path: '/a', component },
      ],
    })
    installRouteViewTransitions(router, () => false)
    await router.push('/')
    await router.push('/a')
    expect(start).not.toHaveBeenCalled()
  })
})

describe('useAnnouncer', () => {
  it('writes polite and assertive messages, re-announcing repeats', async () => {
    const { polite, assertive, announce, clear } = useAnnouncer()
    await announce('Saved')
    expect(polite.value).toBe('Saved')
    await announce('Saved')
    expect(polite.value).toBe('Saved')
    await announce('Failed', 'assertive')
    expect(assertive.value).toBe('Failed')
    clear()
    expect(polite.value).toBe('')
    expect(assertive.value).toBe('')
  })
})

describe('useNavBadges', () => {
  it('sets, clears and formats counts', () => {
    const { badges, setBadge, badgeFor } = useNavBadges()
    const matches = badgeFor('matches')
    setBadge('matches', 4.7)
    expect(badges.matches).toBe(4)
    expect(matches.value).toBe(4)
    setBadge('matches', 0)
    expect(badges.matches).toBeUndefined()
    setBadge('matches', -2)
    expect(matches.value).toBe(0)
    expect(formatBadge(7)).toBe('7')
    expect(formatBadge(120)).toBe('99+')
  })
})

describe('useCommandPalette', () => {
  it('shares open state between callers', () => {
    const a = useCommandPalette()
    const b = useCommandPalette()
    a.show()
    expect(b.open.value).toBe(true)
    b.toggle()
    expect(a.open.value).toBe(false)
    a.toggle()
    a.hide()
    expect(b.open.value).toBe(false)
  })

  it('recognises Ctrl+K and Cmd+K only', () => {
    expect(
      isCommandPaletteShortcut(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true })),
    ).toBe(true)
    expect(
      isCommandPaletteShortcut(new KeyboardEvent('keydown', { key: 'K', metaKey: true })),
    ).toBe(true)
    expect(isCommandPaletteShortcut(new KeyboardEvent('keydown', { key: 'k' }))).toBe(false)
    expect(
      isCommandPaletteShortcut(
        new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, altKey: true }),
      ),
    ).toBe(false)
    expect(
      isCommandPaletteShortcut(
        new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, repeat: true }),
      ),
    ).toBe(false)
  })

  it('detects Apple platforms', () => {
    const spy = vi.spyOn(navigator, 'platform', 'get').mockReturnValue('MacIntel')
    expect(isApplePlatform()).toBe(true)
    spy.mockReturnValue('Win32')
    expect(isApplePlatform()).toBe(false)
  })
})

describe('useLocale', () => {
  it('defaults to Slovenian, persists changes and toggles', () => {
    const { result } = withSetup(useLocale)
    expect(result.locale.value).toBe('sl')
    result.setLocale('en')
    expect(result.locale.value).toBe('en')
    result.toggleLocale()
    expect(result.locale.value).toBe('sl')
  })

  it('pushes the locale into vue-i18n and <html lang>', async () => {
    const { result, i18n } = withSetup(useLocaleEffect)
    expect(i18n.global.locale.value).toBe('sl')
    expect(document.documentElement.lang).toBe('sl')
    result.setLocale('en')
    await nextTick()
    expect(i18n.global.locale.value).toBe('en')
    expect(document.documentElement.lang).toBe('en')
  })
})

describe('useSidebar', () => {
  it('collapses by default on tablets and remembers explicit toggles', async () => {
    setMediaQuery('(min-width: 768px) and (max-width: 1023.98px)', true)
    const { result } = withSetup(useSidebar)
    expect(result.collapsed.value).toBe(true)
    result.toggle()
    await nextTick()
    expect(result.collapsed.value).toBe(false)
    expect(localStorage.getItem('domolov:sidebar-collapsed')).toBe('false')
  })

  it('is expanded on desktop by default', () => {
    expect(withSetup(useSidebar).result.collapsed.value).toBe(false)
  })
})

describe('useOnline', () => {
  it('tracks browser connectivity events', async () => {
    const { result } = withSetup(useOnline)
    expect(result.isOnline.value).toBe(true)
    window.dispatchEvent(new Event('offline'))
    await nextTick()
    expect(result.isOffline.value).toBe(true)
    window.dispatchEvent(new Event('online'))
    await nextTick()
    expect(result.isOnline.value).toBe(true)
  })
})
