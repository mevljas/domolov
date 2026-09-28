import { afterAll, afterEach, beforeAll, vi } from 'vitest'
import { config } from '@vue/test-utils'
import { installMatchMedia, resetMediaQueries } from './media'
import { server } from './msw'

// The app calls the API with relative URLs (baseUrl ''); Node's Request needs an origin.
const NativeRequest = globalThis.Request
class RelativeRequest extends NativeRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    const resolved =
      typeof input === 'string' && input.startsWith('/')
        ? new URL(input, window.location.origin)
        : input
    super(resolved, init)
  }
}
globalThis.Request = RelativeRequest as typeof Request

installMatchMedia()

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
globalThis.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver

Element.prototype.scrollIntoView ??= function scrollIntoView() {}
Element.prototype.hasPointerCapture ??= () => false
Element.prototype.releasePointerCapture ??= () => {}

config.global.stubs = { ...config.global.stubs, transition: false }

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => {
  server.resetHandlers()
  localStorage.clear()
  resetMediaQueries()
  document.documentElement.className = ''
  document.body.innerHTML = ''
  vi.useRealTimers()
})
afterAll(() => server.close())
