/** RFC 9457 Problem Details helpers: parse error bodies and turn them into user-facing messages. */

export interface ProblemDetails {
  type?: string | null
  title?: string | null
  status?: number | null
  detail?: string | null
  instance?: string | null
  /** ASP.NET Core ValidationProblemDetails. */
  errors?: Record<string, string[]>
  [extension: string]: unknown
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null
  readonly retryAfterSeconds: number | null

  constructor(
    status: number,
    problem: ProblemDetails | null,
    retryAfterSeconds: number | null = null,
  ) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
    this.retryAfterSeconds = retryAfterSeconds
  }
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false
  const candidate = value as Record<string, unknown>
  return ['type', 'title', 'status', 'detail'].some((key) => key in candidate)
}

/** Read a problem body from a response if it is JSON; never throws. */
export async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const type = response.headers.get('content-type') ?? ''
  if (!type.includes('json')) return null
  try {
    const body: unknown = await response.clone().json()
    return isProblemDetails(body) ? body : null
  } catch {
    return null
  }
}

/** Parse Retry-After (delta-seconds or HTTP-date), falling back to a `retryAfter` problem extension. */
export function retryAfterSeconds(
  headers: Headers | null | undefined,
  problem?: ProblemDetails | null,
  now: number = Date.now(),
): number | null {
  const header = headers?.get('retry-after')?.trim()
  if (header) {
    if (/^\d+$/.test(header)) return Number.parseInt(header, 10)
    const date = Date.parse(header)
    if (!Number.isNaN(date)) return Math.max(0, Math.ceil((date - now) / 1000))
  }
  const extension = problem?.retryAfter ?? problem?.retryAfterSeconds
  if (typeof extension === 'number' && Number.isFinite(extension))
    return Math.max(0, Math.ceil(extension))
  return null
}

export async function toApiError(response: Response, body?: unknown): Promise<ApiError> {
  const problem = isProblemDetails(body) ? body : await readProblem(response)
  return new ApiError(response.status, problem, retryAfterSeconds(response.headers, problem))
}

export type Translate = (key: string, params?: Record<string, unknown>) => string

/** Turn any thrown value into one short, localized sentence for the user. */
export function problemMessage(error: unknown, t: Translate): string {
  if (error instanceof ApiError) return apiErrorMessage(error, t)
  if (isProblemDetails(error)) {
    return apiErrorMessage(new ApiError(error.status ?? 0, error), t)
  }
  if (error instanceof TypeError) return t('errors.network')
  return t('errors.unknown')
}

function apiErrorMessage(error: ApiError, t: Translate): string {
  const { status, problem, retryAfterSeconds: retry } = error
  const validation = firstValidationMessage(problem)
  switch (true) {
    case status === 401:
      return t('errors.unauthorized')
    case status === 403:
      return t('errors.forbidden')
    case status === 404:
      return t('errors.notFound')
    case status === 429:
      return retry !== null
        ? t('errors.rateLimited', { seconds: retry })
        : t('errors.rateLimitedGeneric')
    case status >= 500:
      return t('errors.server')
    case validation !== null:
      return validation!
    case status === 409:
      return problem?.detail ?? t('errors.conflict')
    case status === 400 || status === 422:
      return problem?.detail ?? problem?.title ?? t('errors.validation')
    default:
      return problem?.detail ?? problem?.title ?? t('errors.unknown')
  }
}

/**
 * Per-field validation messages from a 400 ValidationProblemDetails, keyed in camelCase
 * ("SearchUrl" and "initialRoute.Destination" become "searchUrl" / "initialRoute.destination").
 */
export function fieldErrors(error: unknown): Record<string, string> {
  const problem = error instanceof ApiError ? error.problem : isProblemDetails(error) ? error : null
  const errors = problem?.errors
  const result: Record<string, string> = {}
  if (!errors || typeof errors !== 'object') return result
  for (const [key, messages] of Object.entries(errors)) {
    const first = Array.isArray(messages) ? messages.find((m) => typeof m === 'string' && m) : undefined
    if (!first) continue
    const camel = key
      .split('.')
      .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
      .join('.')
    result[camel] = first
  }
  return result
}

function firstValidationMessage(problem: ProblemDetails | null): string | null {
  const errors = problem?.errors
  if (!errors || typeof errors !== 'object') return null
  for (const messages of Object.values(errors)) {
    const first = Array.isArray(messages) ? messages.find((m) => typeof m === 'string' && m) : null
    if (first) return first
  }
  return null
}
