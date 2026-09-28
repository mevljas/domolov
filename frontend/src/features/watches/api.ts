import { type MaybeRefOrGetter, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { api } from '@/api/client'
import { queryKeys } from '@/api/keys'
import { ApiError } from '@/api/problem'
import { ifMatch, unwrap } from '@/api/request'
import type {
  CreateNotificationRouteRequest,
  CreateWatchRequest,
  NotificationRoute,
  ScanRun,
  UpdateNotificationRouteRequest,
  UpdateWatchRequest,
  Watch,
} from '@/api/types'
import { useToast } from '@/shared/composables/useToast'

export function fetchWatches() {
  return unwrap(api.GET('/api/watches'))
}

export function useWatches() {
  return useQuery({ queryKey: queryKeys.watches.list(), queryFn: fetchWatches })
}

export function useWatch(id: MaybeRefOrGetter<string>) {
  const client = useQueryClient()
  return useQuery({
    queryKey: () => queryKeys.watches.detail(toValue(id)),
    queryFn: () => unwrap(api.GET('/api/watches/{id}', { params: { path: { id: toValue(id) } } })),
    placeholderData: () =>
      client.getQueryData<Watch[]>(queryKeys.watches.list())?.find((w) => w.id === toValue(id)),
  })
}

/** Write a fresh Watch into the list and detail caches. */
export function storeWatch(client: QueryClient, watch: Watch) {
  client.setQueryData(queryKeys.watches.detail(watch.id), watch)
  client.setQueryData<Watch[]>(queryKeys.watches.list(), (list) =>
    list ? list.map((w) => (w.id === watch.id ? watch : w)) : list,
  )
}

function invalidateWatchViews(client: QueryClient) {
  void client.invalidateQueries({ queryKey: queryKeys.watches.all })
  void client.invalidateQueries({ queryKey: queryKeys.dashboard })
}

/** 412 means the Watch changed elsewhere: refetch and tell the user. */
function useConflictHandler() {
  const client = useQueryClient()
  const toast = useToast()
  const { t } = useI18n()
  return (error: unknown, watchId: string) => {
    if (error instanceof ApiError && error.status === 412) {
      toast.warning(t('watches.changedElsewhere'))
      void client.invalidateQueries({ queryKey: queryKeys.watches.detail(watchId) })
      void client.invalidateQueries({ queryKey: queryKeys.watches.list() })
    }
  }
}

export function useCreateWatch() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateWatchRequest) => unwrap(api.POST('/api/watches', { body })),
    onSuccess: (watch) => {
      client.setQueryData(queryKeys.watches.detail(watch.id), watch)
      invalidateWatchViews(client)
      void client.invalidateQueries({ queryKey: queryKeys.scans.all })
    },
  })
}

export interface UpdateWatchVariables {
  watch: Pick<Watch, 'id' | 'version'>
  changes: UpdateWatchRequest
}

export function useUpdateWatch() {
  const client = useQueryClient()
  const onConflict = useConflictHandler()
  return useMutation({
    mutationFn: ({ watch, changes }: UpdateWatchVariables) =>
      unwrap(
        api.PATCH('/api/watches/{id}', {
          params: { path: { id: watch.id } },
          body: changes,
          headers: ifMatch(watch.version),
        }),
      ),
    onMutate: async ({ watch, changes }) => {
      // Pausing is instant in the UI; other edits wait for the server.
      if (changes.isPaused === undefined || changes.isPaused === null) return
      await client.cancelQueries({ queryKey: queryKeys.watches.list() })
      const previous = client.getQueryData<Watch[]>(queryKeys.watches.list())
      client.setQueryData<Watch[]>(queryKeys.watches.list(), (list) =>
        list?.map((w) => (w.id === watch.id ? { ...w, isPaused: changes.isPaused! } : w)),
      )
      return { previous }
    },
    onError: (error, { watch }, context) => {
      if (context?.previous) client.setQueryData(queryKeys.watches.list(), context.previous)
      onConflict(error, watch.id)
    },
    onSuccess: (updated) => {
      storeWatch(client, updated)
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}

export function useDeleteWatch() {
  const client = useQueryClient()
  const onConflict = useConflictHandler()
  return useMutation({
    mutationFn: (watch: Pick<Watch, 'id' | 'version'>) =>
      unwrap(
        api.DELETE('/api/watches/{id}', {
          params: { path: { id: watch.id } },
          headers: ifMatch(watch.version),
        }),
      ),
    onSuccess: (_data, watch) => {
      client.setQueryData<Watch[]>(queryKeys.watches.list(), (list) =>
        list?.filter((w) => w.id !== watch.id),
      )
      client.removeQueries({ queryKey: queryKeys.watches.detail(watch.id) })
      invalidateWatchViews(client)
      void client.invalidateQueries({ queryKey: queryKeys.homes.all })
      void client.invalidateQueries({ queryKey: queryKeys.scans.all })
    },
    onError: (error, watch) => onConflict(error, watch.id),
  })
}

export function useRunWatchNow() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (watchId: string) =>
      unwrap(api.POST('/api/watches/{id}/scans', { params: { path: { id: watchId } } })),
    onSuccess: (scan: ScanRun) => {
      client.setQueryData(queryKeys.scans.detail(scan.id), scan)
      void client.invalidateQueries({ queryKey: queryKeys.scans.lists() })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}

export function checkSearchUrl(url: string, signal?: AbortSignal) {
  return unwrap(api.POST('/api/search-url-checks', { body: { url }, signal }))
}

// ---- notification routes -------------------------------------------------------------------

function patchRoutes(
  client: QueryClient,
  watchId: string,
  update: (routes: NotificationRoute[]) => NotificationRoute[],
) {
  const apply = (watch: Watch | undefined) =>
    watch ? { ...watch, routes: update(watch.routes) } : watch
  client.setQueryData<Watch>(queryKeys.watches.detail(watchId), apply)
  client.setQueryData<Watch[]>(queryKeys.watches.list(), (list) =>
    list?.map((w) => (w.id === watchId ? apply(w)! : w)),
  )
}

export function useCreateRoute(watchId: MaybeRefOrGetter<string>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateNotificationRouteRequest) =>
      unwrap(
        api.POST('/api/watches/{watchId}/notification-routes', {
          params: { path: { watchId: toValue(watchId) } },
          body,
        }),
      ),
    onSuccess: (route) => patchRoutes(client, route.watchId, (routes) => [...routes, route]),
  })
}

export interface UpdateRouteVariables {
  route: Pick<NotificationRoute, 'id' | 'watchId' | 'version'>
  changes: UpdateNotificationRouteRequest
}

export function useUpdateRoute() {
  const client = useQueryClient()
  const onConflict = useConflictHandler()
  return useMutation({
    mutationFn: ({ route, changes }: UpdateRouteVariables) =>
      unwrap(
        api.PATCH('/api/watches/{watchId}/notification-routes/{routeId}', {
          params: { path: { watchId: route.watchId, routeId: route.id } },
          body: changes,
          headers: ifMatch(route.version),
        }),
      ),
    onSuccess: (updated) =>
      patchRoutes(client, updated.watchId, (routes) =>
        routes.map((r) => (r.id === updated.id ? updated : r)),
      ),
    onError: (error, { route }) => onConflict(error, route.watchId),
  })
}

export function useDeleteRoute() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (route: Pick<NotificationRoute, 'id' | 'watchId' | 'version'>) =>
      unwrap(
        api.DELETE('/api/watches/{watchId}/notification-routes/{routeId}', {
          params: { path: { watchId: route.watchId, routeId: route.id } },
          headers: ifMatch(route.version),
        }),
      ),
    onSuccess: (_data, route) =>
      patchRoutes(client, route.watchId, (routes) => routes.filter((r) => r.id !== route.id)),
  })
}

export function useTestRoute() {
  return useMutation({
    mutationFn: (route: Pick<NotificationRoute, 'id' | 'watchId'>) =>
      unwrap(
        api.POST('/api/watches/{watchId}/notification-routes/{routeId}/test-deliveries', {
          params: { path: { watchId: route.watchId, routeId: route.id } },
        }),
      ),
  })
}
