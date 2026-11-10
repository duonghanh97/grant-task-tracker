import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { CreateTaskRequest, Task } from '@/api/types'
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

interface AddTaskVariables {
  request: CreateTaskRequest
  idempotencyKey: string
}

/**
 * Adds a task. Network failures are retried with the same Idempotency-Key, so
 * a request that reached the server but lost its response creates one task.
 */
export function useAddTask(grantId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ request, idempotencyKey }: AddTaskVariables) =>
      (await apiClient.post<Task>(`/grants/${grantId}/tasks`, request, {
        headers: { 'Idempotency-Key': idempotencyKey },
      })).data,
    retry: retryOnNetworkError(2),
    onSettled: () => queryClient.invalidateQueries({ queryKey: grantKeys.all }),
  })
}
