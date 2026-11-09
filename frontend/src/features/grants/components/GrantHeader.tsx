import { AlertTriangle, CheckCircle2, ClipboardList, Clock, Lock } from 'lucide-react'
import type { ReactNode } from 'react'
import { toast } from 'sonner'
import { describeApiError, toApiError } from '@/api/errors'
import type { GrantDetail } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { formatDate } from '@/lib/dates'
import { useCloseGrant } from '../api'
import { GrantStatusBadge } from './GrantStatusBadge'

export function GrantHeader({ grant }: { grant: GrantDetail }) {
  const close = useCloseGrant(grant.id)
  const total = grant.tasks.length
  const overdue = grant.tasks.filter((t) => t.isOverdue).length
  const completed = grant.tasks.filter((t) => t.completedDate !== null).length

  const onClose = () =>
    close.mutate(undefined, {
      onSuccess: () => toast.success('Grant closed', { description: `${grant.referenceNumber} is now closed.` }),
      onError: (error) => {
        // D4: show the server's reason (BR-5 wording), not a generic message.
        const { description } = describeApiError(toApiError(error))
        toast.error('Cannot close grant', { description })
      },
    })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h2 className="text-xl font-semibold">{grant.organisationName}</h2>
            <GrantStatusBadge status={grant.status} />
          </div>
          <p className="text-sm text-muted-foreground">
            {grant.referenceNumber} • Application date: {formatDate(grant.applicationDate)}
          </p>
        </div>
        {grant.status === 'Open' && (
          <Button variant="outline" className="text-destructive" onClick={onClose} disabled={close.isPending}>
            <Lock aria-hidden />
            {close.isPending ? 'Closing…' : 'Close Grant'}
          </Button>
        )}
      </div>

      <dl className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <SummaryCard icon={<ClipboardList className="text-blue-600" />} label="Total Tasks" value={total} />
        <SummaryCard icon={<Clock className="text-blue-600" />} label="Pending" value={grant.pendingTaskCount} />
        <SummaryCard icon={<AlertTriangle className="text-red-600" />} label="Overdue" value={overdue} />
        <SummaryCard icon={<CheckCircle2 className="text-emerald-600" />} label="Completed" value={completed} />
      </dl>
    </div>
  )
}

function SummaryCard({ icon, label, value }: { icon: ReactNode; label: string; value: number }) {
  return (
    <Card className="py-3">
      <CardContent className="flex items-center gap-3 px-4">
        <span aria-hidden>{icon}</span>
        <div>
          <dd className="text-xl font-semibold">{value}</dd>
          <dt className="text-sm text-muted-foreground">{label}</dt>
        </div>
      </CardContent>
    </Card>
  )
}
