import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { api } from '@/api/client'
import { queryKeys } from '@/api/keys'
import { unwrap } from '@/api/request'
import type { ScansQuery } from '@/api/types'

export function fetchScans(query: ScansQuery) {
  return unwrap(api.GET('/api/scans', { params: { query } }))
}

export function useScans(query: MaybeRefOrGetter<ScansQuery>) {
  return useQuery({
    queryKey: computed(() => queryKeys.scans.list(toValue(query))),
    queryFn: () => fetchScans(toValue(query)),
    placeholderData: (previous) => previous,
  })
}

export function useScan(id: MaybeRefOrGetter<string | null>) {
  return useQuery({
    queryKey: computed(() => queryKeys.scans.detail(toValue(id) ?? '')),
    queryFn: () => unwrap(api.GET('/api/scans/{id}', { params: { path: { id: toValue(id)! } } })),
    enabled: () => Boolean(toValue(id)),
  })
}

export function useScanArtifacts(
  id: MaybeRefOrGetter<string | null>,
  enabled: MaybeRefOrGetter<boolean> = true,
) {
  return useQuery({
    queryKey: computed(() => queryKeys.scans.artifacts(toValue(id) ?? '')),
    queryFn: () =>
      unwrap(api.GET('/api/scans/{id}/artifacts', { params: { path: { id: toValue(id)! } } })),
    enabled: () => Boolean(toValue(id)) && toValue(enabled),
  })
}

/** Artifacts are served directly (screenshots are image/png and work as <img src>). */
export function artifactUrl(scanId: string, artifactId: string): string {
  return `/api/scans/${encodeURIComponent(scanId)}/artifacts/${encodeURIComponent(artifactId)}`
}
