import { computed } from 'vue'
import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api } from '@/api/client'
import { toApiError } from '@/api/problem'
import type { components } from '@/api/schema'

export type Session = components['schemas']['SessionResponse']

export const sessionQueryKey = ['session'] as const

const SESSION_STALE_MS = 60_000

/** GET /api/session → the session when signed in, `null` on 401. Other failures throw ApiError. */
export async function fetchSession(): Promise<Session | null> {
  const { data, error, response } = await api.GET('/api/session')
  if (response.status === 401) return null
  if (!response.ok || !data) throw await toApiError(response, error)
  return data
}

export function sessionQueryOptions() {
  return {
    queryKey: sessionQueryKey,
    queryFn: fetchSession,
    staleTime: SESSION_STALE_MS,
    retry: false,
  } as const
}

/** Used by the router guard: cached when fresh, otherwise one round-trip. */
export function ensureSession(client: QueryClient): Promise<Session | null> {
  return client.fetchQuery(sessionQueryOptions())
}

export function useSession() {
  const query = useQuery(sessionQueryOptions())
  return {
    session: query.data,
    isAuthenticated: computed(() => Boolean(query.data.value)),
    isPending: query.isPending,
  }
}

export function useSignIn() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (password: string) => {
      const { error, response } = await api.POST('/api/session', { body: { password } })
      if (!response.ok) throw await toApiError(response, error)
    },
    onSuccess: async () => {
      await client.fetchQuery({ ...sessionQueryOptions(), staleTime: 0 })
    },
  })
}

export function useSignOut() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      await api.DELETE('/api/session')
    },
    onSettled: () => {
      client.clear()
      client.setQueryData(sessionQueryKey, null)
    },
  })
}
