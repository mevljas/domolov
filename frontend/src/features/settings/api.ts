import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api } from '@/api/client'
import { queryKeys } from '@/api/keys'
import { unwrap } from '@/api/request'

export function useServerSettings() {
  return useQuery({
    queryKey: queryKeys.settings,
    queryFn: () => unwrap(api.GET('/api/settings')),
    staleTime: 5 * 60_000,
  })
}

export function useSessions() {
  return useQuery({ queryKey: queryKeys.sessions, queryFn: () => unwrap(api.GET('/api/sessions')) })
}

export function useRevokeSession() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      unwrap(api.DELETE('/api/sessions/{id}', { params: { path: { id } } })),
    onSuccess: () => void client.invalidateQueries({ queryKey: queryKeys.sessions }),
  })
}

export function useSignOutEverywhere() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/sessions')),
    onSettled: () => {
      client.clear()
      client.setQueryData(queryKeys.session, null)
    },
  })
}

export function useStorage() {
  return useQuery({ queryKey: queryKeys.storage, queryFn: () => unwrap(api.GET('/api/storage')) })
}

export function useRunCleanup() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/storage/cleanups')),
    onSuccess: () => {
      // The worker runs it asynchronously; refresh a little later.
      setTimeout(() => void client.invalidateQueries({ queryKey: queryKeys.storage }), 3000)
    },
  })
}

export function useDeleteAllListings() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/listings')),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.homes.all })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
      void client.invalidateQueries({ queryKey: queryKeys.bookmarks })
      void client.invalidateQueries({ queryKey: queryKeys.matches.all })
      void client.invalidateQueries({ queryKey: queryKeys.storage })
    },
  })
}

export function useUpsertPushSubscription() {
  return useMutation({
    mutationFn: (body: { endpoint: string; p256dh: string; auth: string }) =>
      unwrap(api.POST('/api/push-subscriptions', { body })),
  })
}

export function useDeletePushSubscription() {
  return useMutation({
    mutationFn: (id: string) =>
      unwrap(api.DELETE('/api/push-subscriptions/{id}', { params: { path: { id } } })),
  })
}
