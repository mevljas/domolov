type Listener = (event: MediaQueryListEvent) => void

const mediaState = new Map<string, boolean>()
const mediaListeners = new Map<string, Set<Listener>>()

/** Flip a media query in tests, e.g. setMediaQuery('(prefers-color-scheme: dark)', true). */
export function setMediaQuery(query: string, matches: boolean) {
  mediaState.set(query, matches)
  for (const listener of mediaListeners.get(query) ?? []) {
    listener({ matches, media: query } as MediaQueryListEvent)
  }
}

export function resetMediaQueries() {
  mediaState.clear()
}

export function installMatchMedia() {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: (query: string): MediaQueryList => {
      const listeners = mediaListeners.get(query) ?? new Set<Listener>()
      mediaListeners.set(query, listeners)
      return {
        get matches() {
          return mediaState.get(query) ?? false
        },
        media: query,
        onchange: null,
        addEventListener: (_: string, listener: Listener) => listeners.add(listener),
        removeEventListener: (_: string, listener: Listener) => listeners.delete(listener),
        addListener: (listener: Listener) => listeners.add(listener),
        removeListener: (listener: Listener) => listeners.delete(listener),
        dispatchEvent: () => true,
      } as unknown as MediaQueryList
    },
  })
}
