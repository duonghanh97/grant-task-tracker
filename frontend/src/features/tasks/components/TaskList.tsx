import { toast } from 'sonner'
import { describeApiError, toApiError } from '@/api/errors'
import type { Task } from '@/api/types'
import { useCompleteTask } from '../api'
import { TaskRow } from './TaskRow'

export function TaskList({ tasks }: { tasks: Task[] }) {
  const complete = useCompleteTask()

  const onComplete = (task: Task) =>
    complete.mutate(task.id, {
      onSuccess: () => toast.success('Task completed', { description: `"${task.title}" has been marked as completed.` }),
      onError: (error) => toast.error('Could not complete task', { description: describeApiError(toApiError(error)).description }),
    })

  if (tasks.length === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">This grant has no tasks yet.</p>
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead className="border-b text-muted-foreground">
          <tr>
            <th scope="col" className="py-2 pr-3 pl-2 font-medium">Task</th>
            <th scope="col" className="py-2 pr-3 font-medium">Due date</th>
            <th scope="col" className="py-2 pr-3 font-medium">Priority</th>
            <th scope="col" className="py-2 pr-3 font-medium">Status</th>
            <th scope="col" className="py-2 pr-2 text-right font-medium">Actions</th>
          </tr>
        </thead>
        <tbody>
          {tasks.map((task) => (
            <TaskRow
              key={task.id}
              task={task}
              onComplete={onComplete}
              completing={complete.isPending && complete.variables === task.id}
            />
          ))}
        </tbody>
      </table>
    </div>
  )
}
