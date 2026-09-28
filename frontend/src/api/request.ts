import { toApiError } from './problem'

interface FetchResult<T> {
  data?: T
  error?: unknown
  response: Response
}

/** Resolve an openapi-fetch call to its data, throwing ApiError (with ProblemDetails) on failure. */
export async function unwrap<T>(call: Promise<FetchResult<T>>): Promise<T> {
  const { data, error, response } = await call
  if (!response.ok) throw await toApiError(response, error)
  return data as T
}

/** Weak ETag for optimistic concurrency, built from the resource `version` (docs/api.md). */
export function ifMatch(version: number): { 'If-Match': string } {
  return { 'If-Match': `W/"${version}"` }
}
