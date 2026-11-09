import { useQueryClient } from '@tanstack/react-query'
import { WifiOff } from 'lucide-react'
import { NETWORK_ERROR_MESSAGE } from '@/api/errors'
import { useServerReachable } from '@/app/connectionStatus'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'

/** Tells the officer clearly when the server cannot be reached (OP-3). */
export function ConnectionBanner() {
  const reachable = useServerReachable()
  const queryClient = useQueryClient()

  if (reachable) {
    return null
  }

  return (
    <Alert variant="destructive" className="flex items-center gap-3">
      <WifiOff aria-hidden />
      <div className="flex-1">
        <AlertTitle>Server unreachable</AlertTitle>
        <AlertDescription>{NETWORK_ERROR_MESSAGE}</AlertDescription>
      </div>
      <Button variant="outline" size="sm" onClick={() => void queryClient.refetchQueries({ type: 'active' })}>
        Try again
      </Button>
    </Alert>
  )
}
