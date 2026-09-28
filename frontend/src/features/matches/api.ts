import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { api } from '@/api/client'
import { queryKeys } from '@/api/keys'
import { unwrap } from '@/api/request'
import type { HomeMatch } from '@/api/types'

export function usePossibleMatches() {
  return useQuery({
    queryKey: queryKeys.matches.possible(),
    queryFn: () =>
      unwrap(api.GET('/api/home-matches', { params: { query: { state: 'possible' } } })),
  })
}

export type ReviewDecision = 'confirmed' | 'rejected'

/** Confirm or reject a possible match; the item leaves the queue immediately. */
export function useReviewMatch() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, state }: { id: string; state: ReviewDecision }) =>
      unwrap(api.PATCH('/api/home-matches/{id}', { params: { path: { id } }, body: { state } })),
    onMutate: async ({ id }) => {
      await client.cancelQueries({ queryKey: queryKeys.matches.possible() })
      const previous = client.getQueryData<HomeMatch[]>(queryKeys.matches.possible())
      client.setQueryData<HomeMatch[]>(queryKeys.matches.possible(), (list) =>
        list?.filter((m) => m.id !== id),
      )
      return { previous }
    },
    onError: (_error, _vars, context) => {
      if (context?.previous) client.setQueryData(queryKeys.matches.possible(), context.previous)
    },
    onSettled: () => {
      void client.invalidateQueries({ queryKey: queryKeys.matches.all })
      void client.invalidateQueries({ queryKey: queryKeys.homes.all })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}
