import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { expect, test } from 'vitest'
import { renderApp } from '@/test/render'
import { problem } from '@/test/mockApi'
import { server } from '@/test/server'

test('opens the first grant and lists every grant with its pending-task count', async () => {
  const { router } = renderApp('/')

  expect(await screen.findByRole('heading', { name: 'Riverside Community Garden Inc.' })).toBeInTheDocument()
  const links = within(screen.getByRole('navigation', { name: 'Grants' })).getAllByRole('link')

  expect(links.map((l) => l.textContent)).toEqual([
    'Riverside Community Garden Inc.GR-100001 • 2 pending tasksOpen',
    'Northside Youth Sports ClubGR-100002 • 2 pending tasksOpen',
    'Harbour Arts CollectiveGR-100003 • 0 pending tasksClosed',
    'Hillview Library Friends GroupGR-100004 • 0 pending tasksOpen',
  ])
  expect(router.state.location.pathname).toBe('/grants/1')
  expect(links[0]).toHaveAttribute('aria-current', 'page')
})

test('status tabs show counts and filter the list', async () => {
  const { user } = renderApp('/grants/1')
  const nav = await screen.findByRole('navigation', { name: 'Grants' })
  await within(nav).findAllByRole('link')

  expect(within(nav).getByRole('button', { name: 'All 4' })).toHaveAttribute('aria-pressed', 'true')
  expect(within(nav).getByRole('button', { name: 'Open 3' })).toBeInTheDocument()

  await user.click(within(nav).getByRole('button', { name: 'Closed 1' }))

  expect(within(nav).getAllByRole('link').map((l) => l.textContent)).toEqual([
    'Harbour Arts CollectiveGR-100003 • 0 pending tasksClosed',
  ])
})

test('selecting a grant shows it', async () => {
  const { user, router } = renderApp('/grants/1')
  const nav = await screen.findByRole('navigation', { name: 'Grants' })

  await user.click(await within(nav).findByRole('link', { name: /Northside Youth Sports Club/ }))

  expect(await screen.findByRole('heading', { name: 'Northside Youth Sports Club' })).toBeInTheDocument()
  expect(router.state.location.pathname).toBe('/grants/2')
})

test('shows the error when the grant list fails to load', async () => {
  server.use(http.get('/api/grants', () => problem(500, 'UNEXPECTED_ERROR', 'An error occurred while processing your request.')))
  renderApp('/')

  expect(await screen.findByText('An error occurred while processing your request.')).toBeInTheDocument()
})

test('shows not found for an unknown grant', async () => {
  renderApp('/grants/99')

  expect(await screen.findByText('Grant 99 does not exist.')).toBeInTheDocument()
})

test('empty grant list says so', async () => {
  server.use(http.get('/api/grants', () => HttpResponse.json([])))
  renderApp('/')

  expect(await screen.findByText('There are no grants yet.')).toBeInTheDocument()
})
