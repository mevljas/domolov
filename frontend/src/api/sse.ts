/**
 * Minimal EventSource wrapper with exponential-backoff reconnects.
 * After `fallbackAfter` consecutive failures it calls `onFallback` so callers can switch to
 * polling, keeps retrying at the max delay, and calls `onRecover` once the stream is back.
 * Note: EventSource cannot send custom headers; the cookie is sent (same-origin) but
 * X-Requested-With is not, so SSE endpoints must be safe GETs.
 */

export type StreamState = 'connecting' | 'open' | 'fallback' | 'closed'

export interface EventStreamOptions {
  url: string
  /** Named SSE events to listen for, in addition to the default `message` event. */
  events?: readonly string[]
  onEvent: (type: string, data: unknown, event: MessageEvent) => void
  onStateChange?: (state: StreamState) => void
  onFallback?: () => void
  onRecover?: () => void
  initialDelayMs?: number
  maxDelayMs?: number
  fallbackAfter?: number
  /** Injectable for tests. */
  createSource?: (url: string) => EventSource
  random?: () => number
}

export interface EventStream {
  readonly state: StreamState
  close(): void
}

function parseData(raw: unknown): unknown {
  if (typeof raw !== 'string') return raw
  try {
    return JSON.parse(raw)
  } catch {
    return raw
  }
}

export function backoffDelay(
  attempt: number,
  initial: number,
  max: number,
  random = Math.random,
): number {
  const exponential = Math.min(max, initial * 2 ** Math.max(0, attempt - 1))
  // Full jitter between 50% and 100% of the exponential step.
  return Math.round(exponential * (0.5 + random() * 0.5))
}

export function createEventStream(options: EventStreamOptions): EventStream {
  const {
    url,
    events = [],
    onEvent,
    onStateChange,
    onFallback,
    onRecover,
    initialDelayMs = 1_000,
    maxDelayMs = 30_000,
    fallbackAfter = 3,
    createSource = (u) => new EventSource(u, { withCredentials: true }),
    random = Math.random,
  } = options

  let source: EventSource | null = null
  let timer: ReturnType<typeof setTimeout> | null = null
  let failures = 0
  let state: StreamState = 'connecting'
  let inFallback = false

  const setState = (next: StreamState) => {
    if (state === next) return
    state = next
    onStateChange?.(next)
  }

  const handle = (type: string) => (event: Event) => {
    const message = event as MessageEvent
    onEvent(type, parseData(message.data), message)
  }

  function connect() {
    if (state === 'closed') return
    source = createSource(url)
    source.addEventListener('open', () => {
      failures = 0
      if (inFallback) {
        inFallback = false
        onRecover?.()
      }
      setState('open')
    })
    source.addEventListener('message', handle('message'))
    for (const name of events) source.addEventListener(name, handle(name))
    source.addEventListener('error', () => {
      source?.close()
      source = null
      if (state === 'closed') return
      failures += 1
      if (!inFallback && failures >= fallbackAfter) {
        inFallback = true
        onFallback?.()
      }
      setState(inFallback ? 'fallback' : 'connecting')
      timer = setTimeout(connect, backoffDelay(failures, initialDelayMs, maxDelayMs, random))
    })
  }

  connect()

  return {
    get state() {
      return state
    },
    close() {
      setState('closed')
      if (timer) clearTimeout(timer)
      source?.close()
      source = null
    },
  }
}
