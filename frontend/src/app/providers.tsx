import { QueryClientProvider, type DefaultOptions } from '@tanstack/react-query'
import { useEffect, useState, type ReactNode } from 'react'
import { Toaster } from '@/components/ui/sonner'
import { ConnectionStatus, ConnectionStatusContext } from './connectionStatus'
import { createQueryClient } from './queryClient'

interface AppProvidersProps {
  children: ReactNode
  /** Lets tests shorten retry delays. */
  queryDefaults?: DefaultOptions
}

export function AppProviders({ children, queryDefaults }: AppProvidersProps) {
  const [connection] = useState(() => new ConnectionStatus())
  const [queryClient] = useState(() => createQueryClient(connection, queryDefaults))

  useEffect(() => {
    // The browser knows when it goes offline; refetch as soon as it is back.
    const offline = () => connection.report(false)
    const online = () => void queryClient.refetchQueries({ type: 'active' })
    window.addEventListener('offline', offline)
    window.addEventListener('online', online)
    return () => {
      window.removeEventListener('offline', offline)
      window.removeEventListener('online', online)
    }
  }, [connection, queryClient])

  return (
    <ConnectionStatusContext value={connection}>
      <QueryClientProvider client={queryClient}>
        {children}
        <Toaster position="bottom-right" closeButton richColors />
      </QueryClientProvider>
    </ConnectionStatusContext>
  )
}
