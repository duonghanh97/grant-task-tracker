import { useQuery } from '@tanstack/react-query'
import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { expect, test } from 'vitest'
import { apiClient } from '@/api/client'
import { renderApp, renderWithProviders } from '@/test/render'
import { server } from '@/test/server'
import { ConnectionBanner } from './ConnectionBanner'

function GrantsProbe() {
  const query = useQuery({ queryKey: ['probe'], queryFn: async () => (await apiClient.get<unknown[]>('/grants')).data })
  return <p>{query.isSuccess ? `Loaded ${query.data.length}` : 'Waiting'}</p>
}

test('shows the banner when the server cannot be reached and hides it after a successful retry', async () => {
  let calls = 0
  server.use(http.get('/api/grants', () => (++calls <= 3 ? HttpResponse.error() : HttpResponse.json([]))))
  const { user } = renderWithProviders(<><ConnectionBanner /><GrantsProbe /></>)

  expect(await screen.findByText('Server unreachable')).toBeInTheDocument()
  expect(screen.getByText(/Cannot reach the server/)).toBeInTheDocument()
  expect(calls).toBe(3) // the first attempt plus two automatic retries

  await user.click(screen.getByRole('button', { name: 'Try again' }))

  expect(await screen.findByText('Loaded 0')).toBeInTheDocument()
  expect(screen.queryByText('Server unreachable')).not.toBeInTheDocument()
})

test('does not show the banner for errors the server returned', async () => {
  server.use(http.get('/api/grants', () =>
    HttpResponse.json({ status: 500, code: 'UNEXPECTED_ERROR', title: 'Error' }, { status: 500 })))
  renderWithProviders(<><ConnectionBanner /><GrantsProbe /></>)

  await new Promise((resolve) => setTimeout(resolve, 50))

  expect(screen.queryByText('Server unreachable')).not.toBeInTheDocument()
})

test('the app shell renders the page', async () => {
  renderApp('/')
  expect(await screen.findByRole('heading', { name: 'Grants' })).toBeInTheDocument()
})
