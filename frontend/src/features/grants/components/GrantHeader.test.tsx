import { screen, within } from '@testing-library/react'
import { expect, test } from 'vitest'
import { renderApp } from '@/test/render'

test('shows grant details and task summary', async () => {
  renderApp('/grants/1')

  expect(await screen.findByRole('heading', { name: 'Riverside Community Garden Inc.' })).toBeInTheDocument()
  expect(screen.getByText('GR-100001 • Application date: 09 Sep 2026')).toBeInTheDocument()
  const summary = (label: string) => within(screen.getByText(label).parentElement!).getByRole('definition').textContent
  expect(summary('Total Tasks')).toBe('3')
  expect(summary('Pending')).toBe('2')
  expect(summary('Overdue')).toBe('1')
  expect(summary('Completed')).toBe('1')
})

// BR-5 and D4: the server's reason is shown, not a generic message.
test('closing a grant with a pending High-priority task shows the server reason', async () => {
  const { user } = renderApp('/grants/1')

  await user.click(await screen.findByRole('button', { name: 'Close Grant' }))

  expect(await screen.findByText('Cannot close grant')).toBeInTheDocument()
  expect(screen.getByText('Grant GR-100001 has 1 pending High-priority task.')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Close Grant' })).toBeEnabled()
})

test('closing an eligible grant updates its status and the list', async () => {
  const { user } = renderApp('/grants/2')

  await user.click(await screen.findByRole('button', { name: 'Close Grant' }))

  expect(await screen.findByText('Grant closed')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Close Grant' })).not.toBeInTheDocument()
  const nav = screen.getByRole('navigation', { name: 'Grants' })
  expect(await within(nav).findByRole('button', { name: 'Closed 2' })).toBeInTheDocument()
})

test('a closed grant has no Close button', async () => {
  renderApp('/grants/3')

  expect(await screen.findByRole('heading', { name: 'Harbour Arts Collective' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Close Grant' })).not.toBeInTheDocument()
})
