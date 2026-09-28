import { describe, expect, it } from 'vitest'
import { loginLocation, safeRedirect } from './redirect'

describe('safeRedirect', () => {
  it.each([
    ['/homes', '/homes'],
    ['/watches/42?tab=routes#top', '/watches/42?tab=routes#top'],
    [['/matches', '/other'], '/matches'],
  ])('accepts in-app path %j', (input, expected) => {
    expect(safeRedirect(input)).toBe(expected)
  })

  it.each([
    [undefined],
    [null],
    [''],
    ['https://evil.example'],
    ['//evil.example/path'],
    ['/\\evil.example'],
    ['javascript:alert(1)'],
    ['homes'],
    ['/login'],
    ['/login?redirect=/'],
    ['/ho\nmes'],
    [42],
  ])('rejects %j', (input) => {
    expect(safeRedirect(input)).toBe('/')
  })

  it('uses the provided fallback', () => {
    expect(safeRedirect('//x', '/homes')).toBe('/homes')
  })
})

describe('loginLocation', () => {
  it('carries the current path as redirect', () => {
    expect(loginLocation('/homes?page=2')).toEqual({
      path: '/login',
      query: { redirect: '/homes?page=2' },
    })
  })

  it('omits redirect for the root and unsafe paths', () => {
    expect(loginLocation('/')).toEqual({ path: '/login' })
    expect(loginLocation('//evil')).toEqual({ path: '/login' })
  })
})
