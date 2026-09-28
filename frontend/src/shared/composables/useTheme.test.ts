import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { setMediaQuery } from '@/test/media'
import { withSetup } from '@/test/with-setup'
import { PREFERENCES_STORAGE_KEY } from '@/stores/preferences'
import { applyTheme, THEME_COLORS, useTheme, useThemeEffect } from './useTheme'

const DARK_QUERY = '(prefers-color-scheme: dark)'

describe('useTheme', () => {
  it('defaults to the system preference', () => {
    const { result } = withSetup(useTheme)
    expect(result.preference.value).toBe('system')
    expect(result.resolved.value).toBe('light')
  })

  it('follows the OS colour scheme while set to system', async () => {
    setMediaQuery(DARK_QUERY, true)
    const { result } = withSetup(useTheme)
    expect(result.resolved.value).toBe('dark')

    setMediaQuery(DARK_QUERY, false)
    await nextTick()
    expect(result.resolved.value).toBe('light')
  })

  it('persists an explicit choice to localStorage', async () => {
    const { result } = withSetup(useTheme)
    result.setTheme('dark')
    await nextTick()

    expect(result.isDark.value).toBe(true)
    expect(JSON.parse(localStorage.getItem(PREFERENCES_STORAGE_KEY)!)).toMatchObject({
      theme: 'dark',
    })
  })

  it('restores the saved choice and ignores invalid stored values', () => {
    localStorage.setItem(PREFERENCES_STORAGE_KEY, JSON.stringify({ theme: 'dark', locale: 'en' }))
    expect(withSetup(useTheme).result.preference.value).toBe('dark')

    localStorage.setItem(PREFERENCES_STORAGE_KEY, JSON.stringify({ theme: 'neon', locale: 'xx' }))
    expect(withSetup(useTheme).result.preference.value).toBe('system')
  })

  it('cycles system → light → dark → system and toggles by what is shown', () => {
    const { result } = withSetup(useTheme)
    result.cycleTheme()
    expect(result.preference.value).toBe('light')
    result.cycleTheme()
    expect(result.preference.value).toBe('dark')
    result.cycleTheme()
    expect(result.preference.value).toBe('system')

    result.toggleTheme()
    expect(result.preference.value).toBe('dark')
    result.toggleTheme()
    expect(result.preference.value).toBe('light')
  })
})

describe('applyTheme / useThemeEffect', () => {
  it('toggles the dark class, color-scheme and theme-color meta', () => {
    const meta = document.createElement('meta')
    meta.name = 'theme-color'
    document.head.append(meta)

    applyTheme('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(document.documentElement.style.colorScheme).toBe('dark')
    expect(meta.content).toBe(THEME_COLORS.dark)

    applyTheme('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(meta.content).toBe(THEME_COLORS.light)
    meta.remove()
  })

  it('keeps <html> in sync with the preference', async () => {
    const { result } = withSetup(useThemeEffect)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    result.setTheme('dark')
    await nextTick()
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
})
