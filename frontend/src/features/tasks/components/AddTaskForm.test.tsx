import { fireEvent, screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { expect, test } from 'vitest'
import { problem, received } from '@/test/mockApi'
import { renderApp } from '@/test/render'
import { server } from '@/test/server'

async function openDialog(route = '/grants/1') {
  const app = renderApp(route)
  await app.user.click(await screen.findByRole('button', { name: 'Add Task' }))
  return { ...app, dialog: await screen.findByRole('dialog', { name: 'Add Task' }) }
}

async function fill(user: ReturnType<typeof renderApp>['user'], dialog: HTMLElement, title: string, dueDate: string, priority = 'High') {
  const titleInput = within(dialog).getByLabelText(/Task Title/)
  await user.clear(titleInput)
  if (title) await user.type(titleInput, title)
  fireEvent.change(within(dialog).getByLabelText(/Due Date/), { target: { value: dueDate } })
  await user.selectOptions(within(dialog).getByLabelText(/Priority/), priority)
}

test('adds a task: sends an Idempotency-Key, closes the dialog and shows the new row', async () => {
  const { user, dialog } = await openDialog()

  await fill(user, dialog, 'Prepare project report', '2026-11-20')
  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))

  expect(await screen.findByText('Task added')).toBeInTheDocument()
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  expect(await screen.findByRole('cell', { name: /Prepare project report/ })).toBeInTheDocument()
  expect(received).toHaveLength(1)
  expect(received[0].idempotencyKey).toMatch(/^[0-9a-f-]{36}$/)
})

test('client-side validation blocks invalid input without calling the API', async () => {
  const { user, dialog } = await openDialog()

  await fill(user, dialog, '', '2026-11-07')
  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))

  expect(await within(dialog).findByText('Title is required.')).toBeInTheDocument()
  expect(within(dialog).getByText('Due date cannot be in the past.')).toBeInTheDocument()
  expect(within(dialog).getByLabelText(/Task Title/)).toHaveAttribute('aria-invalid', 'true')
  expect(received).toHaveLength(0)
})

test('title over 100 characters is rejected on the client', async () => {
  const { user, dialog } = await openDialog()

  await fill(user, dialog, 'a'.repeat(101), '2026-11-20')
  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))

  expect(await within(dialog).findByText('Title must be 100 characters or fewer.')).toBeInTheDocument()
  expect(received).toHaveLength(0)
})

// The server is authoritative, e.g. when its business date is ahead of the browser's.
test('server validation errors appear on their fields', async () => {
  server.use(http.post('/api/grants/:id/tasks', () =>
    problem(400, 'VALIDATION_FAILED', 'One or more validation errors occurred.', undefined,
      { dueDate: ['Due date cannot be in the past.'] })))
  const { user, dialog } = await openDialog()

  await fill(user, dialog, 'Task', '2026-11-08')
  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))

  expect(await within(dialog).findByText('Due date cannot be in the past.')).toBeInTheDocument()
  expect(within(dialog).getByLabelText(/Due Date/)).toHaveAttribute('aria-invalid', 'true')
  expect(screen.getByRole('dialog')).toBeInTheDocument()
})

// BR-1 from the server: the grant was closed by someone else after the page loaded.
test('business-rule errors from the server are shown in the dialog', async () => {
  server.use(http.post('/api/grants/:id/tasks', () =>
    problem(409, 'GRANT_CLOSED', 'Grant is closed', 'Grant GR-100001 is closed, so tasks cannot be added to it.')))
  const { user, dialog } = await openDialog()

  await fill(user, dialog, 'Task', '2026-11-20')
  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))

  expect(await within(dialog).findByText('Grant is closed')).toBeInTheDocument()
  expect(within(dialog).getByText('Grant GR-100001 is closed, so tasks cannot be added to it.')).toBeInTheDocument()
})

// OP-3: every retry of the same submission carries the same key, so only one task is created.
test('retries after network failures reuse the same Idempotency-Key', async () => {
  const keys: (string | null)[] = []
  let failuresLeft = 3 // the first attempt and both automatic retries fail
  server.use(http.post('/api/grants/:id/tasks', ({ request }) => {
    keys.push(request.headers.get('Idempotency-Key'))
    if (failuresLeft > 0) {
      failuresLeft--
      return HttpResponse.error()
    }
    return undefined // fall through to the fake API
  }))
  const { user, dialog } = await openDialog()
  await fill(user, dialog, 'Prepare project report', '2026-11-20')

  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))
  expect(await within(dialog).findByText('Task not saved: server unreachable', {}, { timeout: 8000 })).toBeInTheDocument()
  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))

  expect(await screen.findByText('Task added')).toBeInTheDocument()
  expect(keys).toHaveLength(4)
  expect(new Set(keys).size).toBe(1)
  expect(screen.getAllByRole('cell', { name: /Prepare project report/ })).toHaveLength(1)
}, 15_000)

test('changing the values after a failure uses a new key', async () => {
  const keys: (string | null)[] = []
  server.use(http.post('/api/grants/:id/tasks', ({ request }) => {
    keys.push(request.headers.get('Idempotency-Key'))
    return keys.length === 1 ? problem(409, 'CONCURRENCY_CONFLICT', 'Conflict', 'Try again.') : undefined
  }))
  const { user, dialog } = await openDialog()

  await fill(user, dialog, 'First title', '2026-11-20')
  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))
  await within(dialog).findByText('Try again.')
  await fill(user, dialog, 'Second title', '2026-11-20')
  await user.click(within(dialog).getByRole('button', { name: 'Save Task' }))

  expect(await screen.findByText('Task added')).toBeInTheDocument()
  expect(keys[0]).not.toBe(keys[1])
})

test('Add Task is disabled for a closed grant', async () => {
  renderApp('/grants/3')

  expect(await screen.findByRole('button', { name: 'Add Task' })).toBeDisabled()
  expect(screen.getByText('Closed grants cannot take new tasks.')).toBeInTheDocument()
})
