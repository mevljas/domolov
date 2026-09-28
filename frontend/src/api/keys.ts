/** Query key factory: every server-state cache entry is addressed through here. */
export const queryKeys = {
  session: ['session'] as const,
  sessions: ['sessions'] as const,
  dashboard: ['dashboard'] as const,
  settings: ['settings'] as const,
  storage: ['storage'] as const,
  bookmarks: ['bookmarks'] as const,
  watches: {
    all: ['watches'] as const,
    list: () => ['watches', 'list'] as const,
    detail: (id: string) => ['watches', 'detail', id] as const,
  },
  homes: {
    all: ['homes'] as const,
    feeds: () => ['homes', 'feed'] as const,
    feed: (query: object) => ['homes', 'feed', query] as const,
    detail: (id: string) => ['homes', 'detail', id] as const,
    search: (q: string) => ['homes', 'search', q] as const,
  },
  listings: {
    detail: (id: string) => ['listings', id] as const,
  },
  matches: {
    all: ['home-matches'] as const,
    possible: () => ['home-matches', 'possible'] as const,
  },
  scans: {
    all: ['scans'] as const,
    lists: () => ['scans', 'list'] as const,
    list: (query: object) => ['scans', 'list', query] as const,
    detail: (id: string) => ['scans', 'detail', id] as const,
    artifacts: (id: string) => ['scans', 'artifacts', id] as const,
  },
}
