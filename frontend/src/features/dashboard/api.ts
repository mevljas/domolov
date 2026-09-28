import { useQuery } from '@tanstack/vue-query'
import { api } from '@/api/client'
import { queryKeys } from '@/api/keys'
import { unwrap } from '@/api/request'

export function useDashboard() {
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: () => unwrap(api.GET('/api/dashboard')),
  })
}
