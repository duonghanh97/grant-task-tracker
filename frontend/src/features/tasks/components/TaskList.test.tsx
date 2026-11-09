import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { expect, test } from 'vitest'
import { problem } from '@/test/mockApi'
import { renderApp } from '@/test/render'
import { server } from '@/test/server'

const row = (title: string) => screen.getByRole('cell', { name: new RegExp(title) }).closest('tr')!

test('lists tasks with due date, priority and status, highlighting overdue tasks', async () => {
  renderApp('/grants/1')
  await screen.findByRole('cell', { name: /Verify bank details/ })

  const overdue = row('Verify bank details')
  expect(overdue).toHaveAttribute('data-overdue')
  expect(overdue).toHaveClass('bg-red-50')
  expect(within(overdue).getByText('Overdue')).toBeInTheDocument()
  expect(within(overdue).getByText('05 Nov 2026')).toBeInTheDocument()
  expect(within(overdue).getByText('High')).toBeInTheDocument()

  const pending = row('Request quote for tools')
  expect(pending).not.toHaveAttribute('data-overdue')
  expect(within(pending).getByText('Pending')).toBeInTheDocument()

  const completed = row('Check eligibility documents')
  expect(completed).not.toHaveAttribute('data-overdue')
  expect(within(completed).getByText('Completed')).toBeInTheDocument()
})

test('only pending tasks have a Complete button', async () => {
  renderApp('/grants/1')
  await screen.findByRole('cell', { name: /Verify bank details/ })

  expect(screen.getAllByRole('button', { name: /^Complete "/ }).map((b) => b.getAttribute('aria-label'))).toEqual([
    'Complete "Verify bank details"',
    'Complete "Request quote for tools"',
  ])
})

test('completing a task shows a confirmation and updates the row and counts', async () => {
  const { user } = renderApp('/grants/1')

  await user.click(await screen.findByRole('button', { name: 'Complete "Verify bank details"' }))

  expect(await screen.findByText('Task completed')).toBeInTheDocument()
  expect(screen.getByText('"Verify bank details" has been marked as completed.')).toBeInTheDocument()
  expect(await within(row('Verify bank details')).findByText('Completed')).toBeInTheDocument()
  expect(row('Verify bank details')).not.toHaveAttribute('data-overdue')
  expect(screen.queryByRole('button', { name: 'Complete "Verify bank details"' })).not.toBeInTheDocument()
  const nav = screen.getByRole('navigation', { name: 'Grants' })
  expect(await within(nav).findByText(/GR-100001 • 1 pending task$/)).toBeInTheDocument()
})

test('completion is retried after a network failure', async () => {
  let attempts = 0
  server.use(http.put('/api/tasks/:id/complete', () => {
    attempts++
    return attempts === 1 ? HttpResponse.error() : undefined
  }))
  const { user } = renderApp('/grants/1')

  await user.click(await screen.findByRole('button', { name: 'Complete "Verify bank details"' }))

  expect(await within(row('Verify bank details')).findByText('Completed', {}, { timeout: 4000 })).toBeInTheDocument()
  expect(attempts).toBe(2)
  expect(screen.getByText('Task completed')).toBeInTheDocument()
})

test('a completion error from the server is shown', async () => {
  server.use(http.put('/api/tasks/:id/complete', () =>
    problem(409, 'CONCURRENCY_CONFLICT', 'The grant was changed by someone else',
      'Someone else changed this grant at the same time. Reload it and try again.')))
  const { user } = renderApp('/grants/1')

  await user.click(await screen.findByRole('button', { name: 'Complete "Verify bank details"' }))

  expect(await screen.findByText('Could not complete task')).toBeInTheDocument()
  expect(screen.getByText('Someone else changed this grant at the same time. Reload it and try again.')).toBeInTheDocument()
})

test('a grant without tasks says so', async () => {
  renderApp('/grants/4')

  expect(await screen.findByText('This grant has no tasks yet.')).toBeInTheDocument()
})
