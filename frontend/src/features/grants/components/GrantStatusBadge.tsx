import type { GrantStatus } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

export function GrantStatusBadge({ status }: { status: GrantStatus }) {
  return (
    <Badge
      variant="outline"
      className={cn(
        status === 'Open' ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : 'bg-muted text-muted-foreground',
      )}
    >
      {status}
    </Badge>
  )
}
