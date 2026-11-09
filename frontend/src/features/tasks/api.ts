import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { Task } from '@/api/types'
import { retryOnNetworkError } from '@/app/queryClient'
import { grantKeys } from '@/features/grants/api'

/** Completing is idempotent on the server (BR-4), so network failures are retried. */
export function useCompleteTask() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (taskId: number) => (await apiClient.put<Task>(`/tasks/${taskId}/complete`)).data,
    retry: retryOnNetworkError(2),
    onSettled: () => queryClient.invalidateQueries({ queryKey: grantKeys.all }),
  })
}
