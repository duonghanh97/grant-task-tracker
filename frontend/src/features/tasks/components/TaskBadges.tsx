import type { TaskPriority } from '@/api/types'
import type { TaskState } from '../taskState'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

const priorityStyles: Record<TaskPriority, string> = {
  High: 'border-red-200 bg-red-50 text-red-700',
  Medium: 'border-blue-200 bg-blue-50 text-blue-700',
  Low: 'border-slate-200 bg-slate-50 text-slate-700',
}

export function PriorityBadge({ priority }: { priority: TaskPriority }) {
  return <Badge variant="outline" className={priorityStyles[priority]}>{priority}</Badge>
}

const stateStyles: Record<TaskState, string> = {
  Completed: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  Overdue: 'border-red-300 bg-red-100 text-red-800',
  Pending: 'border-blue-200 bg-blue-50 text-blue-700',
}

export function TaskStateBadge({ state }: { state: TaskState }) {
  return <Badge variant="outline" className={cn(stateStyles[state])}>{state}</Badge>
}
