import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import {
  type InfiniteData,
  type QueryClient,
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/vue-query'
import { api } from '@/api/client'
import { queryKeys } from '@/api/keys'
import { unwrap } from '@/api/request'
import type {
  Bookmark,
  BookmarkBoard,
  BookmarkStage,
  Dashboard,
  HomeDetail,
  HomesQuery,
  HomeSummary,
  PagedHomes,
} from '@/api/types'

export const FEED_PAGE_SIZE = 24

export function fetchHomes(query: HomesQuery) {
  return unwrap(api.GET('/api/homes', { params: { query } }))
}

export function useHomesFeed(query: MaybeRefOrGetter<HomesQuery>) {
  return useInfiniteQuery({
    queryKey: computed(() => queryKeys.homes.feed(toValue(query))),
    queryFn: ({ pageParam }) =>
      fetchHomes({ ...toValue(query), Page: pageParam, PageSize: FEED_PAGE_SIZE }),
    initialPageParam: 1,
    getNextPageParam: (last: PagedHomes) =>
      last.page * last.pageSize < last.total ? last.page + 1 : undefined,
  })
}

export function useHomeSearch(q: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.homes.search(toValue(q).trim())),
    queryFn: () =>
      fetchHomes({ Q: toValue(q).trim(), PageSize: 8, Dismissed: 'include', Status: 'all' }),
    enabled: () => toValue(q).trim().length >= 2,
    staleTime: 60_000,
  })
}

export function useHomeDetail(id: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.homes.detail(toValue(id))),
    queryFn: () => unwrap(api.GET('/api/homes/{id}', { params: { path: { id: toValue(id) } } })),
  })
}

export function useListingDetail(id: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.listings.detail(toValue(id))),
    queryFn: () => unwrap(api.GET('/api/listings/{id}', { params: { path: { id: toValue(id) } } })),
  })
}

// ---- cache helpers ---------------------------------------------------------------------------

type FeedData = InfiniteData<PagedHomes, number>

/** Apply a change to one Home everywhere it is cached (feeds, detail, dashboard, board). */
export function patchHome(
  client: QueryClient,
  id: string,
  update: (home: HomeSummary) => HomeSummary,
) {
  const map = (home: HomeSummary) => (home.id === id ? update(home) : home)
  client.setQueriesData<FeedData>({ queryKey: queryKeys.homes.feeds() }, (data) =>
    data ? { ...data, pages: data.pages.map((p) => ({ ...p, items: p.items.map(map) })) } : data,
  )
  client.setQueryData<HomeDetail>(queryKeys.homes.detail(id), (detail) =>
    detail ? { ...detail, home: update(detail.home) } : detail,
  )
  client.setQueryData<Dashboard>(queryKeys.dashboard, (d) =>
    d ? { ...d, unseen: d.unseen.map(map), priceDrops: d.priceDrops.map(map) } : d,
  )
  client.setQueryData<BookmarkBoard>(queryKeys.bookmarks, (board) =>
    board ? { columns: board.columns.map((c) => ({ ...c, homes: c.homes.map(map) })) } : board,
  )
}

/** Drop a Home from every feed page (dismiss); returns snapshots for rollback. */
export function removeHomeFromFeeds(client: QueryClient, id: string) {
  const snapshots = client.getQueriesData<FeedData>({ queryKey: queryKeys.homes.feeds() })
  client.setQueriesData<FeedData>({ queryKey: queryKeys.homes.feeds() }, (data) =>
    data
      ? {
          ...data,
          pages: data.pages.map((p) => {
            const items = p.items.filter((h) => h.id !== id)
            return { ...p, items, total: p.total - (p.items.length - items.length) }
          }),
        }
      : data,
  )
  return snapshots
}

function restoreSnapshots(client: QueryClient, snapshots: [readonly unknown[], unknown][]) {
  for (const [key, data] of snapshots) client.setQueryData(key, data)
}

// ---- bookmarks -------------------------------------------------------------------------------

export interface SetBookmarkVariables {
  homeId: string
  stage: BookmarkStage
  note: string | null
}

function optimisticBookmark(
  existing: Bookmark | null,
  stage: BookmarkStage,
  note: string | null,
): Bookmark {
  const now = new Date().toISOString()
  return { stage, note, createdAt: existing?.createdAt ?? now, updatedAt: now }
}

export function useSetBookmark() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ homeId, stage, note }: SetBookmarkVariables) =>
      unwrap(
        api.PUT('/api/homes/{id}/bookmark', {
          params: { path: { id: homeId } },
          body: { stage, note },
        }),
      ),
    onMutate: ({ homeId, stage, note }) => {
      patchHome(client, homeId, (h) => ({
        ...h,
        bookmark: optimisticBookmark(h.bookmark, stage, note),
      }))
    },
    onSuccess: (bookmark, { homeId }) => patchHome(client, homeId, (h) => ({ ...h, bookmark })),
    onError: (_error, { homeId }) =>
      void client.invalidateQueries({ queryKey: queryKeys.homes.detail(homeId) }),
    onSettled: () => {
      void client.invalidateQueries({ queryKey: queryKeys.bookmarks })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}

export function useRemoveBookmark() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (homeId: string) =>
      unwrap(api.DELETE('/api/homes/{id}/bookmark', { params: { path: { id: homeId } } })),
    onMutate: (homeId) => {
      let previous: Bookmark | null = null
      patchHome(client, homeId, (h) => {
        previous = h.bookmark
        return { ...h, bookmark: null }
      })
      client.setQueryData<BookmarkBoard>(queryKeys.bookmarks, (board) =>
        board
          ? {
              columns: board.columns.map((c) => ({
                ...c,
                homes: c.homes.filter((h) => h.id !== homeId),
              })),
            }
          : board,
      )
      return { previous }
    },
    onError: (_error, homeId, context) => {
      if (context?.previous)
        patchHome(client, homeId, (h) => ({ ...h, bookmark: context.previous }))
    },
    onSettled: () => {
      void client.invalidateQueries({ queryKey: queryKeys.bookmarks })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}

/** Move a Bookmark to another stage on the board (optimistic column move). */
export function useMoveBookmark() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ home, stage }: { home: HomeSummary; stage: BookmarkStage }) =>
      unwrap(
        api.PUT('/api/homes/{id}/bookmark', {
          params: { path: { id: home.id } },
          body: { stage, note: home.bookmark?.note ?? null },
        }),
      ),
    onMutate: async ({ home, stage }) => {
      await client.cancelQueries({ queryKey: queryKeys.bookmarks })
      const previous = client.getQueryData<BookmarkBoard>(queryKeys.bookmarks)
      const moved: HomeSummary = {
        ...home,
        bookmark: optimisticBookmark(home.bookmark, stage, home.bookmark?.note ?? null),
      }
      client.setQueryData<BookmarkBoard>(queryKeys.bookmarks, (board) =>
        board ? moveOnBoard(board, moved, stage) : board,
      )
      patchHome(client, home.id, () => moved)
      return { previous }
    },
    onError: (_error, _vars, context) => {
      if (context?.previous) client.setQueryData(queryKeys.bookmarks, context.previous)
    },
    onSettled: () => void client.invalidateQueries({ queryKey: queryKeys.bookmarks }),
  })
}

export function moveOnBoard(
  board: BookmarkBoard,
  home: HomeSummary,
  stage: BookmarkStage,
): BookmarkBoard {
  return {
    columns: board.columns.map((column) => {
      const homes = column.homes.filter((h) => h.id !== home.id)
      return { ...column, homes: column.stage === stage ? [home, ...homes] : homes }
    }),
  }
}

// ---- dismissal & seen ------------------------------------------------------------------------

export function useDismissHome() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (homeId: string) =>
      unwrap(api.PUT('/api/homes/{id}/dismissal', { params: { path: { id: homeId } } })),
    onMutate: async (homeId) => {
      await client.cancelQueries({ queryKey: queryKeys.homes.feeds() })
      const snapshots = removeHomeFromFeeds(client, homeId)
      patchHome(client, homeId, (h) => ({ ...h, isDismissed: true, bookmark: null }))
      return { snapshots }
    },
    onError: (_error, _homeId, context) => {
      if (context) restoreSnapshots(client, context.snapshots)
    },
    onSettled: () => {
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
      void client.invalidateQueries({ queryKey: queryKeys.bookmarks })
    },
  })
}

export function useRestoreHome() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (homeId: string) =>
      unwrap(api.DELETE('/api/homes/{id}/dismissal', { params: { path: { id: homeId } } })),
    onSuccess: (_data, homeId) => {
      patchHome(client, homeId, (h) => ({ ...h, isDismissed: false }))
      void client.invalidateQueries({ queryKey: queryKeys.homes.all })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}

export function useMarkSeen() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (homeId: string) =>
      unwrap(api.PUT('/api/homes/{id}/seen', { params: { path: { id: homeId } } })),
    onMutate: (homeId) => patchHome(client, homeId, (h) => ({ ...h, isUnseen: false })),
    onSettled: () => void client.invalidateQueries({ queryKey: queryKeys.dashboard }),
  })
}

export function useMarkAllSeen() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (before?: string) =>
      unwrap(api.POST('/api/homes/seen', { body: { before: before ?? null } })),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.homes.all })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}

// ---- manual linking --------------------------------------------------------------------------

export function useLinkListing(homeId: MaybeRefOrGetter<string>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (listingId: string) =>
      unwrap(
        api.POST('/api/homes/{id}/listings', {
          params: { path: { id: toValue(homeId) } },
          body: { listingId },
        }),
      ),
    onSuccess: (detail) => {
      client.setQueryData(queryKeys.homes.detail(detail.home.id), detail)
      void client.invalidateQueries({ queryKey: queryKeys.homes.feeds() })
      void client.invalidateQueries({ queryKey: queryKeys.matches.all })
    },
  })
}

export function useUnlinkListing(homeId: MaybeRefOrGetter<string>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (listingId: string) =>
      unwrap(
        api.DELETE('/api/homes/{id}/listings/{listingId}', {
          params: { path: { id: toValue(homeId), listingId } },
        }),
      ),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.homes.all })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}
