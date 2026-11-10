import { AlertTriangle, Check } from 'lucide-react'
import type { Task } from '@/api/types'
import { Button } from '@/components/ui/button'
import { formatDate } from '@/lib/dates'
import { cn } from '@/lib/utils'
import { taskState } from '../taskState'
import { PriorityBadge, TaskStateBadge } from './TaskBadges'

interface TaskRowProps {
  task: Task
  onComplete: (task: Task) => void
  completing: boolean
}

export function TaskRow({ task, onComplete, completing }: TaskRowProps) {
  const state = taskState(task)
  const overdue = state === 'Overdue'

  return (
    <tr data-overdue={overdue || undefined} className={cn('border-b last:border-0', overdue && 'bg-red-50')}>
      <td className="py-3 pr-3 pl-2 font-medium">
        <span className="flex items-center gap-2">
          {overdue && <AlertTriangle className="size-4 shrink-0 text-red-600" aria-label="Overdue" />}
          {task.title}
        </span>
      </td>
      <td className={cn('py-3 pr-3 whitespace-nowrap', overdue && 'font-semibold text-red-700')}>
        {formatDate(task.dueDate)}
      </td>
      <td className="py-3 pr-3"><PriorityBadge priority={task.priority} /></td>
      <td className="py-3 pr-3">
        <TaskStateBadge state={state} />
        {task.completedDate && (
          <span className="ml-2 text-xs text-muted-foreground">on {formatDate(task.completedDate)}</span>
        )}
      </td>
      <td className="py-3 pr-2 text-right">
        {task.completedDate === null && (
          <Button
            size="sm"
            variant="outline"
            onClick={() => onComplete(task)}
            disabled={completing}
            aria-label={`Complete "${task.title}"`}
          >
            <Check aria-hidden />
            {completing ? 'Completing…' : 'Complete'}
          </Button>
        )}
      </td>
    </tr>
  )
}
