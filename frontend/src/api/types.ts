// Mirrors the API's JSON contracts (backend Features/*/…Contracts.cs).
// Dates are calendar dates as 'yyyy-MM-dd' strings.

export type GrantStatus = 'Open' | 'Closed'
export type TaskPriority = 'Low' | 'Medium' | 'High'

export const TASK_PRIORITIES: readonly TaskPriority[] = ['Low', 'Medium', 'High']

export interface GrantSummary {
  id: number
  referenceNumber: string
  organisationName: string
  applicationDate: string
  status: GrantStatus
  pendingTaskCount: number
}

export interface Task {
  id: number
  grantId: number
  title: string
  dueDate: string
  priority: TaskPriority
  completedDate: string | null
  /** Computed by the server with its business date (BR-6). */
  isOverdue: boolean
}

export interface GrantDetail extends GrantSummary {
  /** Ordered by due date, then id. */
  tasks: Task[]
}

export interface CreateTaskRequest {
  title: string
  dueDate: string
  priority: TaskPriority
}

/** RFC 9457 problem details as returned by every API error. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}
