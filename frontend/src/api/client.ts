import createClient, { type Client, type Middleware } from 'openapi-fetch'
import type { paths } from './schema'
import { loginLocation } from '@/shared/lib/redirect'

/** Sent on every request; the backend rejects cookie-authenticated calls without it (CSRF guard). */
export const REQUESTED_WITH_HEADER = 'X-Requested-With'
export const REQUESTED_WITH_VALUE = 'domolov'

export const SESSION_PATH = '/api/session'

export type UnauthorizedHandler = (currentPath: string) => void

function currentPath(): string {
  const { pathname, search, hash } = window.location
  return `${pathname}${search}${hash}`
}

function defaultUnauthorizedHandler(path: string) {
  const target = loginLocation(path)
  const query = target.query ? `?redirect=${encodeURIComponent(target.query.redirect)}` : ''
  window.location.assign(`${target.path}${query}`)
}

let unauthorizedHandler: UnauthorizedHandler = defaultUnauthorizedHandler

/** Replace the 401 reaction (the app wires this to the router so redirects stay client-side). */
export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler ?? defaultUnauthorizedHandler
}

/**
 * The session endpoint answers 401 as a normal outcome (signed out / wrong password);
 * everything else returning 401 means the session expired mid-use.
 */
function isSessionRequest(request: Request): boolean {
  return new URL(request.url, window.location.origin).pathname === SESSION_PATH
}

export const authMiddleware: Middleware = {
  onRequest({ request }) {
    request.headers.set(REQUESTED_WITH_HEADER, REQUESTED_WITH_VALUE)
    return request
  },
  onResponse({ request, response }) {
    if (response.status === 401 && !isSessionRequest(request)) {
      const path = currentPath()
      if (!path.startsWith('/login')) unauthorizedHandler(path)
    }
    return response
  },
}

export function createApiClient<TPaths extends object = paths>(baseUrl = ''): Client<TPaths> {
  const client = createClient<TPaths>({
    baseUrl,
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
    // Resolve fetch lazily so test interceptors and polyfills installed later are honoured.
    fetch: (request) => globalThis.fetch(request),
  })
  client.use(authMiddleware)
  return client
}

export const api = createApiClient()
