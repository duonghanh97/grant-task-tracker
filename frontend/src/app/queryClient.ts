import { MutationCache, QueryCache, QueryClient, type DefaultOptions } from '@tanstack/react-query'
import { toApiError } from '@/api/errors'
import type { ConnectionStatus } from './connectionStatus'

/** Retry only when the server could not be reached; other errors are final. */
export function retryOnNetworkError(maxRetries: number) {
  return (failureCount: number, error: unknown) =>
    failureCount < maxRetries && toApiError(error).kind === 'network'
}

export function createQueryClient(connection: ConnectionStatus, overrides: DefaultOptions = {}) {
  const onError = (error: unknown) => {
    if (toApiError(error).kind === 'network') {
      connection.report(false)
    }
  }
  const onSuccess = () => connection.report(true)

  return new QueryClient({
    queryCache: new QueryCache({ onError, onSuccess }),
    mutationCache: new MutationCache({ onError, onSuccess }),
    defaultOptions: {
      ...overrides,
      queries: {
        retry: retryOnNetworkError(2),
        retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 8000),
        staleTime: 15_000,
        ...overrides.queries,
      },
      mutations: {
        // Mutations opt in to retries individually (only idempotent ones).
        retry: false,
        ...overrides.mutations,
      },
    },
  })
}
