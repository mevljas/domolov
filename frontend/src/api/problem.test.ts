import { describe, expect, it } from 'vitest'
import {
  ApiError,
  isProblemDetails,
  problemMessage,
  readProblem,
  retryAfterSeconds,
  toApiError,
  type Translate,
} from './problem'

const t: Translate = (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key)

const problemResponse = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/problem+json', ...headers },
  })

describe('isProblemDetails', () => {
  it('recognises RFC 9457 shaped objects', () => {
    expect(isProblemDetails({ title: 'Bad', status: 400 })).toBe(true)
    expect(isProblemDetails({ detail: 'x' })).toBe(true)
  })

  it('rejects everything else', () => {
    expect(isProblemDetails(null)).toBe(false)
    expect(isProblemDetails('oops')).toBe(false)
    expect(isProblemDetails([])).toBe(false)
    expect(isProblemDetails({ message: 'nope' })).toBe(false)
  })
})

describe('readProblem', () => {
  it('parses problem+json bodies without consuming the response', async () => {
    const response = problemResponse(404, { title: 'Not Found', status: 404 })
    expect(await readProblem(response)).toEqual({ title: 'Not Found', status: 404 })
    expect(response.bodyUsed).toBe(false)
  })

  it('returns null for non-JSON or malformed bodies', async () => {
    expect(await readProblem(new Response('oops', { status: 500 }))).toBeNull()
    expect(
      await readProblem(
        new Response('{bad', {
          status: 500,
          headers: { 'content-type': 'application/json' },
        }),
      ),
    ).toBeNull()
    expect(
      await readProblem(
        new Response('[1]', {
          status: 500,
          headers: { 'content-type': 'application/json' },
        }),
      ),
    ).toBeNull()
  })
})

describe('retryAfterSeconds', () => {
  const now = Date.parse('2026-09-27T12:00:00Z')

  it('reads delta seconds', () => {
    expect(retryAfterSeconds(new Headers({ 'retry-after': '30' }))).toBe(30)
  })

  it('reads HTTP dates relative to now', () => {
    expect(
      retryAfterSeconds(new Headers({ 'retry-after': 'Sun, 27 Sep 2026 12:01:30 GMT' }), null, now),
    ).toBe(90)
    expect(
      retryAfterSeconds(new Headers({ 'retry-after': 'Sun, 27 Sep 2026 11:00:00 GMT' }), null, now),
    ).toBe(0)
  })

  it('falls back to a problem extension', () => {
    expect(retryAfterSeconds(new Headers(), { title: 'x', retryAfter: 12.2 })).toBe(13)
    expect(retryAfterSeconds(undefined, { title: 'x', retryAfterSeconds: 5 })).toBe(5)
  })

  it('returns null when unknown', () => {
    expect(retryAfterSeconds(new Headers({ 'retry-after': 'soon' }))).toBeNull()
    expect(retryAfterSeconds(null)).toBeNull()
  })
})

describe('toApiError', () => {
  it('builds an ApiError from the response and an already-parsed body', async () => {
    const response = new Response(null, { status: 429, headers: { 'retry-after': '7' } })
    const error = await toApiError(response, { title: 'Too Many Requests', status: 429 })
    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(429)
    expect(error.retryAfterSeconds).toBe(7)
    expect(error.message).toBe('Too Many Requests')
  })

  it('reads the body itself when none is given', async () => {
    const error = await toApiError(problemResponse(409, { detail: 'Watch was renamed.' }))
    expect(error.problem?.detail).toBe('Watch was renamed.')
    expect(error.message).toBe('Watch was renamed.')
  })

  it('has a generic message without a problem body', () => {
    expect(new ApiError(502, null).message).toBe('Request failed with status 502')
  })
})

describe('problemMessage', () => {
  it.each([
    [401, 'errors.unauthorized'],
    [403, 'errors.forbidden'],
    [404, 'errors.notFound'],
    [500, 'errors.server'],
    [503, 'errors.server'],
  ])('maps %d to a localized message', (status, key) => {
    expect(problemMessage(new ApiError(status, { detail: 'server text' }), t)).toBe(key)
  })

  it('includes the retry hint for 429 when known', () => {
    expect(problemMessage(new ApiError(429, null, 30), t)).toBe('errors.rateLimited {"seconds":30}')
    expect(problemMessage(new ApiError(429, null), t)).toBe('errors.rateLimitedGeneric')
  })

  it('prefers the first validation message', () => {
    const error = new ApiError(400, {
      title: 'One or more validation errors occurred.',
      errors: {
        searchUrl: ['', 'Search URL must be on nepremicnine.net.'],
        name: ['Required.'],
      },
    })
    expect(problemMessage(error, t)).toBe('Search URL must be on nepremicnine.net.')
  })

  it('uses server detail for conflicts and bad requests, with localized fallbacks', () => {
    expect(problemMessage(new ApiError(409, { detail: 'Already running.' }), t)).toBe(
      'Already running.',
    )
    expect(problemMessage(new ApiError(409, { title: 'Conflict' }), t)).toBe('errors.conflict')
    expect(problemMessage(new ApiError(400, { title: 'Bad cron' }), t)).toBe('Bad cron')
    expect(problemMessage(new ApiError(422, { type: 'about:blank' }), t)).toBe('errors.validation')
    expect(problemMessage(new ApiError(418, { detail: 'Teapot' }), t)).toBe('Teapot')
    expect(problemMessage(new ApiError(418, null), t)).toBe('errors.unknown')
  })

  it('accepts bare problem objects, network errors and unknown values', () => {
    expect(problemMessage({ status: 404, title: 'Not Found' }, t)).toBe('errors.notFound')
    expect(problemMessage({ title: 'Oops' }, t)).toBe('Oops')
    expect(problemMessage(new TypeError('Failed to fetch'), t)).toBe('errors.network')
    expect(problemMessage('boom', t)).toBe('errors.unknown')
  })
})
