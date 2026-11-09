import type { Task } from '@/api/types'

export type TaskState = 'Completed' | 'Overdue' | 'Pending'

/** Overdue comes from the server's isOverdue (BR-6), never from the browser clock. */
export function taskState(task: Task): TaskState {
  if (task.completedDate !== null) return 'Completed'
  return task.isOverdue ? 'Overdue' : 'Pending'
}
