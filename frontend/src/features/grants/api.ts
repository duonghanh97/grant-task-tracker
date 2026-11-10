import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { GrantDetail, GrantSummary } from '@/api/types'

export const grantKeys = {
  all: ['grants'] as const,
  list: () => [...grantKeys.all, 'list'] as const,
  detail: (id: number) => [...grantKeys.all, 'detail', id] as const,
}

/** All grants; the status tabs filter on the client so every tab can show its count (D5). */
export function useGrants() {
  return useQuery({
    queryKey: grantKeys.list(),
    queryFn: async ({ signal }) => (await apiClient.get<GrantSummary[]>('/grants', { signal })).data,
  })
}

export function useGrant(id: number) {
  return useQuery({
    queryKey: grantKeys.detail(id),
    queryFn: async ({ signal }) => (await apiClient.get<GrantDetail>(`/grants/${id}`, { signal })).data,
  })
}

export function useCloseGrant(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => (await apiClient.put<GrantDetail>(`/grants/${id}/close`)).data,
    onSuccess: (grant) => queryClient.setQueryData(grantKeys.detail(id), grant),
    // Refresh after success or failure: a 409 may mean someone else changed the grant.
    onSettled: () => queryClient.invalidateQueries({ queryKey: grantKeys.all }),
  })
}
