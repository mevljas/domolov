import { useQuery } from '@tanstack/vue-query'
import { api } from '@/api/client'
import { queryKeys } from '@/api/keys'
import { unwrap } from '@/api/request'

export function useBookmarkBoard() {
  return useQuery({
    queryKey: queryKeys.bookmarks,
    queryFn: () => unwrap(api.GET('/api/bookmarks')),
  })
}
