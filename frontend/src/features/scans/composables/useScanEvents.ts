import { onScopeDispose, readonly, ref } from 'vue'
import { type QueryClient, useQueryClient } from '@tanstack/vue-query'
import { queryKeys } from '@/api/keys'
import { createEventStream, type EventStream, type StreamState } from '@/api/sse'
import {
  isActiveScan,
  type Dashboard,
  type PagedScanRuns,
  type ScanRun,
  type ScansQuery,
  type Watch,
} from '@/api/types'
import { fetchScans } from '../api'

export const SCAN_EVENTS_URL = '/api/scans/events'
const POLL_INTERVAL_MS = 5_000

const streamState = ref<StreamState>('closed')

function matchesQuery(scan: ScanRun, query: ScansQuery | undefined): boolean {
  if (!query) return true
  if (query.watchId && query.watchId !== scan.watchId) return false
  if (query.status && query.status !== scan.status) return false
  if (query.active !== undefined && query.active !== isActiveScan(scan.status)) return false
  return true
}

function upsertPage(
  page: PagedScanRuns,
  scan: ScanRun,
  query: ScansQuery | undefined,
): PagedScanRuns {
  const index = page.items.findIndex((s) => s.id === scan.id)
  const fits = matchesQuery(scan, query)
  if (index >= 0) {
    const items = fits
      ? page.items.map((s) => (s.id === scan.id ? scan : s))
      : page.items.filter((s) => s.id !== scan.id)
    return { ...page, items, total: fits ? page.total : page.total - 1 }
  }
  if (!fits || (query?.page ?? 1) !== 1) return page
  return { ...page, items: [scan, ...page.items].slice(0, page.pageSize), total: page.total + 1 }
}

function withLastScan<T extends Watch>(watch: T, scan: ScanRun): T {
  if (watch.id !== scan.watchId) return watch
  const newer =
    !watch.lastScan || watch.lastScan.id === scan.id || scan.queuedAt >= watch.lastScan.queuedAt
  return newer ? { ...watch, lastScan: scan } : watch
}

/**
 * Merge one ScanRun (from SSE or polling) into every cache that shows scans.
 * Returns true when the run just finished, so callers can refresh derived data.
 */
export function applyScanRun(client: QueryClient, scan: ScanRun): boolean {
  const previous = client.getQueryData<ScanRun>(queryKeys.scans.detail(scan.id))
  client.setQueryData(queryKeys.scans.detail(scan.id), scan)

  for (const [key, data] of client.getQueriesData<PagedScanRuns>({
    queryKey: queryKeys.scans.lists(),
  })) {
    if (!data) continue
    client.setQueryData(key, upsertPage(data, scan, key[2] as ScansQuery | undefined))
  }

  client.setQueryData<Dashboard>(queryKeys.dashboard, (dashboard) => {
    if (!dashboard) return dashboard
    const others = dashboard.activeScans.filter((s) => s.id !== scan.id)
    return {
      ...dashboard,
      activeScans: isActiveScan(scan.status) ? [scan, ...others] : others,
      watches: dashboard.watches.map((w) => withLastScan(w, scan)),
    }
  })
  client.setQueryData<Watch[]>(queryKeys.watches.list(), (list) =>
    list?.map((w) => withLastScan(w, scan)),
  )
  client.setQueryData<Watch>(queryKeys.watches.detail(scan.watchId), (w) =>
    w ? withLastScan(w, scan) : w,
  )

  const finished = !isActiveScan(scan.status) && (!previous || isActiveScan(previous.status))
  if (finished) {
    for (const key of [
      queryKeys.homes.all,
      queryKeys.dashboard,
      queryKeys.watches.all,
      queryKeys.matches.all,
      queryKeys.bookmarks,
    ]) {
      void client.invalidateQueries({ queryKey: key })
    }
  }
  return finished
}

export interface ScanEventsOptions {
  createStream?: typeof createEventStream
  pollIntervalMs?: number
}

/** Open the app-wide ScanRun stream; polls `GET /api/scans?active=true` while SSE is down. */
export function useScanEvents(options: ScanEventsOptions = {}) {
  const { createStream = createEventStream, pollIntervalMs = POLL_INTERVAL_MS } = options
  const client = useQueryClient()
  let timer: ReturnType<typeof setInterval> | null = null
  let lastActive = new Set<string>()

  async function poll() {
    try {
      const page = await fetchScans({ active: true, pageSize: 50 })
      const active = new Set(page.items.map((s) => s.id))
      page.items.forEach((scan) => applyScanRun(client, scan))
      // Runs that left the active set finished while we were not streaming.
      if ([...lastActive].some((id) => !active.has(id))) {
        void client.invalidateQueries({ queryKey: queryKeys.scans.all })
        void client.invalidateQueries({ queryKey: queryKeys.dashboard })
        void client.invalidateQueries({ queryKey: queryKeys.homes.all })
      }
      lastActive = active
    } catch {
      // Keep polling; the offline banner covers connectivity.
    }
  }

  function startPolling() {
    if (timer) return
    void poll()
    timer = setInterval(() => void poll(), pollIntervalMs)
  }

  function stopPolling() {
    if (timer) clearInterval(timer)
    timer = null
  }

  const stream: EventStream = createStream({
    url: SCAN_EVENTS_URL,
    events: ['ready', 'scanRun', 'ping'],
    onEvent: (type, data) => {
      if (type === 'scanRun' && data && typeof data === 'object')
        applyScanRun(client, data as ScanRun)
    },
    onStateChange: (state) => (streamState.value = state),
    onFallback: startPolling,
    onRecover: () => {
      stopPolling()
      void client.invalidateQueries({ queryKey: queryKeys.scans.all })
    },
  })

  onScopeDispose(() => {
    stream.close()
    stopPolling()
    streamState.value = 'closed'
  })

  return { state: readonly(streamState) }
}

/** Current stream state for UI indicators (open / connecting / fallback). */
export function useScanStreamState() {
  return readonly(streamState)
}
