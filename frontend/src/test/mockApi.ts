import { addDays, format, parseISO } from 'date-fns'
import { http, HttpResponse } from 'msw'
import type { CreateTaskRequest, GrantDetail, GrantStatus, Task, TaskPriority } from '@/api/types'

/** Frozen "today" for frontend tests (see setup.ts). */
export const TODAY = '2026-11-08'

const day = (offset: number) => format(addDays(parseISO(TODAY), offset), 'yyyy-MM-dd')

interface MockGrant {
  id: number
  referenceNumber: string
  organisationName: string
  applicationDate: string
  status: GrantStatus
  tasks: Omit<Task, 'grantId' | 'isOverdue'>[]
}

/** The SPECS §9 seed data, relative to TODAY. */
function seed(): MockGrant[] {
  return [
    {
      id: 1, referenceNumber: 'GR-100001', organisationName: 'Riverside Community Garden Inc.',
      applicationDate: day(-60), status: 'Open',
      tasks: [
        { id: 1, title: 'Verify bank details', dueDate: day(-3), priority: 'High', completedDate: null },
        { id: 2, title: 'Request quote for tools', dueDate: day(5), priority: 'Medium', completedDate: null },
        { id: 3, title: 'Check eligibility documents', dueDate: day(-50), priority: 'High', completedDate: day(-50) },
      ],
    },
    {
      id: 2, referenceNumber: 'GR-100002', organisationName: 'Northside Youth Sports Club',
      applicationDate: day(-20), status: 'Open',
      tasks: [
        { id: 4, title: 'Review progress report', dueDate: day(-1), priority: 'Low', completedDate: null },
        { id: 5, title: 'Schedule site visit', dueDate: day(10), priority: 'Medium', completedDate: null },
      ],
    },
    {
      id: 3, referenceNumber: 'GR-100003', organisationName: 'Harbour Arts Collective',
      applicationDate: day(-200), status: 'Closed',
      tasks: [{ id: 6, title: 'Final acquittal review', dueDate: day(-150), priority: 'High', completedDate: day(-150) }],
    },
    {
      id: 4, referenceNumber: 'GR-100004', organisationName: 'Hillview Library Friends Group',
      applicationDate: day(-5), status: 'Open', tasks: [],
    },
  ]
}

let grants = seed()
let nextTaskId = 100
const idempotency = new Map<string, number>()

export function resetMockApi() {
  grants = seed()
  nextTaskId = 100
  idempotency.clear()
}

/** Requests the fake API received, for assertions (e.g. idempotency keys). */
export const received: { method: string; path: string; idempotencyKey: string | null }[] = []

const toTask = (grant: MockGrant, task: MockGrant['tasks'][number]): Task => ({
  ...task, grantId: grant.id, isOverdue: task.completedDate === null && task.dueDate < TODAY,
})

const toDetail = (g: MockGrant): GrantDetail => ({
  id: g.id, referenceNumber: g.referenceNumber, organisationName: g.organisationName,
  applicationDate: g.applicationDate, status: g.status,
  pendingTaskCount: g.tasks.filter((t) => t.completedDate === null).length,
  tasks: [...g.tasks].sort((a, b) => a.dueDate.localeCompare(b.dueDate) || a.id - b.id).map((t) => toTask(g, t)),
})

export function problem(status: number, code: string, title: string, detail?: string, errors?: Record<string, string[]>) {
  return HttpResponse.json(
    { type: `https://granttracker/errors/${code.toLowerCase()}`, title, status, detail, code, errors, traceId: '00-test' },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )
}

const notFound = (what: 'Grant' | 'Task', id: number) =>
  problem(404, `${what.toUpperCase()}_NOT_FOUND`, `${what} not found`, `${what} ${id} does not exist.`)

/** A small in-memory imitation of the API, including BR-1, BR-2, BR-3 and BR-5. */
export const mockApiHandlers = [
  http.get('/api/grants', () =>
    HttpResponse.json(grants.map((g) => {
      const { tasks: _tasks, ...summary } = toDetail(g)
      return summary
    })),
  ),

  http.get('/api/grants/:id', ({ params }) => {
    const grant = grants.find((g) => g.id === Number(params.id))
    return grant ? HttpResponse.json(toDetail(grant)) : notFound('Grant', Number(params.id))
  }),

  http.put('/api/grants/:id/close', ({ params }) => {
    const grant = grants.find((g) => g.id === Number(params.id))
    if (!grant) return notFound('Grant', Number(params.id))
    const pendingHigh = grant.tasks.filter((t) => t.priority === 'High' && t.completedDate === null).length
    if (grant.status === 'Open' && pendingHigh > 0) {
      return problem(409, 'GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS', 'Grant cannot be closed',
        `Grant ${grant.referenceNumber} has ${pendingHigh} pending High-priority ${pendingHigh === 1 ? 'task' : 'tasks'}.`)
    }
    grant.status = 'Closed'
    return HttpResponse.json(toDetail(grant))
  }),

  http.post('/api/grants/:id/tasks', async ({ params, request }) => {
    const key = request.headers.get('Idempotency-Key')
    received.push({ method: 'POST', path: new URL(request.url).pathname, idempotencyKey: key })
    const grant = grants.find((g) => g.id === Number(params.id))
    if (!key) return problem(400, 'IDEMPOTENCY_KEY_MISSING', 'Idempotency-Key header is required')
    if (!grant) return notFound('Grant', Number(params.id))
    const replayId = idempotency.get(key)
    if (replayId !== undefined) {
      const task = grant.tasks.find((t) => t.id === replayId)!
      return HttpResponse.json(toTask(grant, task), { status: 201, headers: { 'Idempotent-Replayed': 'true' } })
    }
    if (grant.status === 'Closed') {
      return problem(409, 'GRANT_CLOSED', 'Grant is closed', `Grant ${grant.referenceNumber} is closed, so tasks cannot be added to it.`)
    }
    const body = (await request.json()) as Partial<CreateTaskRequest>
    const errors: Record<string, string[]> = {}
    const title = body.title?.trim() ?? ''
    if (!title) errors.title = ['Title is required.']
    else if (title.length > 100) errors.title = ['Title must be 100 characters or fewer.']
    if (!body.dueDate) errors.dueDate = ['Due date is required.']
    else if (body.dueDate < TODAY) errors.dueDate = ['Due date cannot be in the past.']
    if (Object.keys(errors).length > 0) {
      return problem(400, 'VALIDATION_FAILED', 'One or more validation errors occurred.', undefined, errors)
    }
    const task = { id: nextTaskId++, title, dueDate: body.dueDate!, priority: body.priority as TaskPriority, completedDate: null }
    grant.tasks.push(task)
    idempotency.set(key, task.id)
    return HttpResponse.json(toTask(grant, task), { status: 201 })
  }),

  http.put('/api/tasks/:id/complete', ({ params }) => {
    const id = Number(params.id)
    const grant = grants.find((g) => g.tasks.some((t) => t.id === id))
    if (!grant) return notFound('Task', id)
    const task = grant.tasks.find((t) => t.id === id)!
    task.completedDate ??= TODAY
    return HttpResponse.json(toTask(grant, task))
  }),
]
