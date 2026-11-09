import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest'
import { received, resetMockApi, TODAY } from './mockApi'
import { server } from './server'

// Any request without a handler fails the test, so no API call goes unnoticed.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))

beforeEach(() => {
  // Freeze only Date, so tests never depend on the real date but timers still run.
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date(`${TODAY}T09:00:00`))
  resetMockApi()
  received.length = 0
})

afterEach(() => {
  server.resetHandlers()
  cleanup()
  vi.useRealTimers()
})

afterAll(() => server.close())
