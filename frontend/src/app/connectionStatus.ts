import { createContext, useContext, useSyncExternalStore } from 'react'

/**
 * Whether the API was reachable on the last request. Updated by the query
 * client's global callbacks; read by ConnectionBanner (OP-3).
 */
export class ConnectionStatus {
  private reachable = true
  private readonly listeners = new Set<() => void>()

  isReachable = () => this.reachable

  subscribe = (listener: () => void) => {
    this.listeners.add(listener)
    return () => {
      this.listeners.delete(listener)
    }
  }

  report(reachable: boolean) {
    if (this.reachable !== reachable) {
      this.reachable = reachable
      this.listeners.forEach((listener) => listener())
    }
  }
}

export const ConnectionStatusContext = createContext<ConnectionStatus | null>(null)

export function useServerReachable(): boolean {
  const status = useContext(ConnectionStatusContext)
  if (!status) {
    throw new Error('useServerReachable must be used inside AppProviders')
  }
  return useSyncExternalStore(status.subscribe, status.isReachable)
}
