import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { AppProviders } from '@/app/providers'
import { routes } from '@/app/router'

const fastRetries = { queries: { retryDelay: 0 } }

/** Renders the whole app at a URL, with fresh providers and instant retries. */
export function renderApp(route = '/') {
  const router = createMemoryRouter(routes, { initialEntries: [route] })
  const user = userEvent.setup()
  const result = render(
    <AppProviders queryDefaults={fastRetries}>
      <RouterProvider router={router} />
    </AppProviders>,
  )
  return { ...result, router, user }
}

/** Renders a component inside the app providers only (no router). */
export function renderWithProviders(ui: ReactNode) {
  const user = userEvent.setup()
  return { ...render(<AppProviders queryDefaults={fastRetries}>{ui}</AppProviders>), user }
}
