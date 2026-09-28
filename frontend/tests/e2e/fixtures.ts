import { test as base, expect, type Page } from '@playwright/test'

export const PASSWORD = 'correct horse battery staple'

const SESSION = {
  name: 'admin',
  sessionId: '5b0f6a2e-3b7c-4b8a-9d8e-1f2a3b4c5d6e',
  createdAt: '2026-09-27T10:00:00Z',
  expiresAt: '2026-10-27T10:00:00Z',
}

const problem = (status: number, title: string) => ({
  status,
  contentType: 'application/problem+json',
  body: JSON.stringify({ type: 'about:blank', title, status }),
})

export interface MockApi {
  signedIn: boolean
  requests: { method: string; path: string; requestedWith: string | null }[]
}

/** Mock the backend with page.route: session endpoints plus a 404 for everything else. */
export async function mockApi(page: Page, { signedIn = false } = {}): Promise<MockApi> {
  const state: MockApi = { signedIn, requests: [] }

  await page.route('**/api/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    state.requests.push({
      method: request.method(),
      path: url.pathname,
      requestedWith: await request.headerValue('x-requested-with'),
    })

    if (url.pathname !== '/api/session') {
      return route.fulfill(problem(404, 'Not Found'))
    }
    switch (request.method()) {
      case 'GET':
        return state.signedIn
          ? route.fulfill({ status: 200, json: SESSION })
          : route.fulfill(problem(401, 'Unauthorized'))
      case 'POST': {
        const body = request.postDataJSON() as { password?: string }
        if (body.password === PASSWORD) {
          state.signedIn = true
          return route.fulfill({ status: 204 })
        }
        if (body.password === 'too-many') {
          return route.fulfill({
            ...problem(429, 'Too Many Requests'),
            headers: { 'Retry-After': '42' },
          })
        }
        return route.fulfill(problem(401, 'Unauthorized'))
      }
      case 'DELETE':
        state.signedIn = false
        return route.fulfill({ status: 204 })
      default:
        return route.fulfill(problem(405, 'Method Not Allowed'))
    }
  })

  return state
}

/** Start with English UI and/or a theme already chosen. */
export async function presetPreferences(
  page: Page,
  prefs: { locale?: 'sl' | 'en'; theme?: string },
) {
  await page.addInitScript((value) => {
    if (!localStorage.getItem('domolov:preferences')) {
      localStorage.setItem(
        'domolov:preferences',
        JSON.stringify({ theme: 'system', locale: 'sl', ...value }),
      )
    }
  }, prefs)
}

/** Playwright's built-in `isMobile` fixture distinguishes the desktop and Pixel 7 projects. */
export const test = base

export { expect, type Page }
