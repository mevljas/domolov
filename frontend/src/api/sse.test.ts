import { afterEach, describe, expect, it, vi } from 'vitest'
import { backoffDelay, createEventStream } from './sse'

class FakeEventSource {
  static instances: FakeEventSource[] = []
  listeners = new Map<string, ((event: Event) => void)[]>()
  closed = false

  constructor(public url: string) {
    FakeEventSource.instances.push(this)
  }

  addEventListener(type: string, listener: (event: Event) => void) {
    this.listeners.set(type, [...(this.listeners.get(type) ?? []), listener])
  }

  emit(type: string, data?: string) {
    const event =
      type === 'open' || type === 'error' ? new Event(type) : new MessageEvent(type, { data })
    for (const listener of this.listeners.get(type) ?? []) listener(event)
  }

  close() {
    this.closed = true
  }
}

const create = (url: string) => new FakeEventSource(url) as unknown as EventSource
const latest = () => FakeEventSource.instances.at(-1)!

describe('backoffDelay', () => {
  it('grows exponentially with jitter and caps at the maximum', () => {
    expect(backoffDelay(1, 1000, 30_000, () => 1)).toBe(1000)
    expect(backoffDelay(3, 1000, 30_000, () => 1)).toBe(4000)
    expect(backoffDelay(3, 1000, 30_000, () => 0)).toBe(2000)
    expect(backoffDelay(10, 1000, 30_000, () => 1)).toBe(30_000)
  })
})

describe('createEventStream', () => {
  afterEach(() => {
    FakeEventSource.instances = []
    vi.useRealTimers()
  })

  it('parses JSON messages from default and named events', () => {
    const onEvent = vi.fn()
    const states: string[] = []
    createEventStream({
      url: '/api/events',
      events: ['scan'],
      onEvent,
      onStateChange: (s) => states.push(s),
      createSource: create,
    })

    latest().emit('open')
    latest().emit('message', 'plain text')
    latest().emit('scan', '{"id":1}')

    expect(states).toEqual(['open'])
    expect(onEvent).toHaveBeenNthCalledWith(1, 'message', 'plain text', expect.any(MessageEvent))
    expect(onEvent).toHaveBeenNthCalledWith(2, 'scan', { id: 1 }, expect.any(MessageEvent))
  })

  it('reconnects with backoff, falls back to polling, then recovers', () => {
    vi.useFakeTimers()
    const onFallback = vi.fn()
    const onRecover = vi.fn()
    const stream = createEventStream({
      url: '/api/events',
      onEvent: vi.fn(),
      onFallback,
      onRecover,
      fallbackAfter: 2,
      initialDelayMs: 100,
      random: () => 1,
      createSource: create,
    })

    latest().emit('error')
    expect(stream.state).toBe('connecting')
    expect(FakeEventSource.instances[0]!.closed).toBe(true)
    vi.advanceTimersByTime(100)
    expect(FakeEventSource.instances).toHaveLength(2)

    latest().emit('error')
    expect(onFallback).toHaveBeenCalledOnce()
    expect(stream.state).toBe('fallback')
    vi.advanceTimersByTime(200)

    latest().emit('open')
    expect(onRecover).toHaveBeenCalledOnce()
    expect(stream.state).toBe('open')
  })

  it('stops reconnecting once closed', () => {
    vi.useFakeTimers()
    const stream = createEventStream({
      url: '/x',
      onEvent: vi.fn(),
      createSource: create,
      random: () => 1,
    })
    latest().emit('error')
    stream.close()
    vi.advanceTimersByTime(60_000)
    expect(FakeEventSource.instances).toHaveLength(1)
    expect(stream.state).toBe('closed')
  })
})
