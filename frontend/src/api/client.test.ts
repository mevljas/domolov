import { afterEach, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw'
import {
  api,
  createApiClient,
  REQUESTED_WITH_HEADER,
  REQUESTED_WITH_VALUE,
  setUnauthorizedHandler,
} from './client'

interface TestPaths {
  '/api/watches': {
    get: {
      responses: {
        200: { content: { 'application/json': { id: string }[] } }
        401: { content: { 'application/problem+json': { title: string } } }
      }
    }
  }
}

const problem401 = () =>
  HttpResponse.json(
    { title: 'Unauthorized', status: 401 },
    { status: 401, headers: { 'content-type': 'application/problem+json' } },
  )

describe('api client', () => {
  afterEach(() => {
    setUnauthorizedHandler(null)
    window.history.replaceState(null, '', '/')
  })

  it('sends X-Requested-With and same-origin credentials on every request', async () => {
    let seen: Request | undefined
    server.use(
      http.get('*/api/session', ({ request }) => {
        seen = request
        return HttpResponse.json({
          name: 'admin',
          sessionId: 'x',
          createdAt: '',
          expiresAt: '',
        })
      }),
    )

    const { data } = await api.GET('/api/session')

    expect(data?.name).toBe('admin')
    expect(seen?.headers.get(REQUESTED_WITH_HEADER)).toBe(REQUESTED_WITH_VALUE)
    expect(seen?.credentials).toBe('same-origin')
  })

  it('redirects to login with the current location when an API call returns 401', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    window.history.replaceState(null, '', '/watches/7?tab=routes')
    server.use(http.get('*/api/watches', problem401))

    const client = createApiClient<TestPaths>()
    const { response, error } = await client.GET('/api/watches')

    expect(response.status).toBe(401)
    expect(error).toEqual({ title: 'Unauthorized', status: 401 })
    expect(handler).toHaveBeenCalledWith('/watches/7?tab=routes')
  })

  it('does not treat the session probe or a wrong password as an expired session', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    server.use(http.get('*/api/session', problem401), http.post('*/api/session', problem401))

    await api.GET('/api/session')
    await api.POST('/api/session', { body: { password: 'wrong' } })

    expect(handler).not.toHaveBeenCalled()
  })

  it('does not redirect again while already on the login page', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    window.history.replaceState(null, '', '/login?redirect=/homes')
    server.use(http.get('*/api/watches', problem401))

    await createApiClient<TestPaths>().GET('/api/watches')

    expect(handler).not.toHaveBeenCalled()
  })

  it('falls back to a full-page redirect when no handler is registered', async () => {
    const assign = vi.fn()
    const original = window.location
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: {
        ...original,
        pathname: '/homes',
        search: '?q=1',
        hash: '',
        origin: original.origin,
        assign,
      },
    })
    server.use(http.get('*/api/watches', problem401))

    try {
      await createApiClient<TestPaths>().GET('/api/watches')
    } finally {
      Object.defineProperty(window, 'location', { configurable: true, value: original })
    }

    expect(assign).toHaveBeenCalledWith('/login?redirect=%2Fhomes%3Fq%3D1')
  })
})
