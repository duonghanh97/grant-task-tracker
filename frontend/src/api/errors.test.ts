import axios from 'axios'
import { delay, http, HttpResponse } from 'msw'
import { describe, expect, test } from 'vitest'
import { server } from '@/test/server'
import { apiClient } from './client'
import { describeApiError, NETWORK_ERROR_MESSAGE, toApiError } from './errors'

async function failureOf(request: Promise<unknown>) {
  try {
    await request
  } catch (error) {
    return toApiError(error)
  }
  throw new Error('Expected the request to fail')
}

const problem = (status: number, body: Record<string, unknown>) =>
  HttpResponse.json({ status, traceId: '00-abc', ...body }, { status, headers: { 'Content-Type': 'application/problem+json' } })

describe('toApiError', () => {
  test('network failure becomes a network error', async () => {
    server.use(http.get('/api/grants', () => HttpResponse.error()))

    expect(await failureOf(apiClient.get('/grants'))).toEqual({ kind: 'network', message: NETWORK_ERROR_MESSAGE })
  })

  test('timeout becomes a network error', async () => {
    server.use(http.get('/api/grants', async () => {
      await delay(200)
      return HttpResponse.json([])
    }))
    const impatient = axios.create({ baseURL: '/api', timeout: 20 })

    expect(await failureOf(impatient.get('/grants'))).toMatchObject({ kind: 'network' })
  })

  test('5xx without problem details (proxy cannot reach the API) becomes a network error', async () => {
    server.use(http.get('/api/grants', () => new HttpResponse(null, { status: 502 })))

    expect(await failureOf(apiClient.get('/grants'))).toMatchObject({ kind: 'network' })
  })

  test('400 keeps field errors', async () => {
    server.use(http.post('/api/grants/1/tasks', () =>
      problem(400, {
        title: 'One or more validation errors occurred.',
        code: 'VALIDATION_FAILED',
        errors: { title: ['Title is required.'], dueDate: ['Due date cannot be in the past.'] },
      })))

    expect(await failureOf(apiClient.post('/grants/1/tasks', {}))).toEqual({
      kind: 'validation',
      code: 'VALIDATION_FAILED',
      title: 'One or more validation errors occurred.',
      detail: undefined,
      fieldErrors: { title: ['Title is required.'], dueDate: ['Due date cannot be in the past.'] },
    })
  })

  test('404 becomes notFound', async () => {
    server.use(http.get('/api/grants/9', () =>
      problem(404, { title: 'Grant not found', detail: 'Grant 9 does not exist.', code: 'GRANT_NOT_FOUND' })))

    expect(await failureOf(apiClient.get('/grants/9'))).toEqual({
      kind: 'notFound', code: 'GRANT_NOT_FOUND', title: 'Grant not found', detail: 'Grant 9 does not exist.',
    })
  })

  test('409 business rule becomes business with the server detail', async () => {
    server.use(http.put('/api/grants/1/close', () =>
      problem(409, {
        title: 'Grant cannot be closed',
        detail: 'Grant GR-100001 has 1 pending High-priority task.',
        code: 'GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS',
      })))

    expect(await failureOf(apiClient.put('/grants/1/close'))).toEqual({
      kind: 'business',
      status: 409,
      code: 'GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS',
      title: 'Grant cannot be closed',
      detail: 'Grant GR-100001 has 1 pending High-priority task.',
    })
  })

  test('500 from the API is unexpected and keeps the trace id', async () => {
    server.use(http.get('/api/grants', () =>
      problem(500, { title: 'An error occurred while processing your request.', code: 'UNEXPECTED_ERROR' })))

    const error = await failureOf(apiClient.get('/grants'))

    expect(error).toMatchObject({ kind: 'unexpected', status: 500, traceId: '00-abc' })
    expect(describeApiError(error).description).toContain('00-abc')
  })

  test('non-HTTP errors are unexpected', () => {
    expect(toApiError(new Error('boom'))).toMatchObject({ kind: 'unexpected', detail: 'boom' })
  })
})

describe('describeApiError', () => {
  test('validation errors list every field message', () => {
    const description = describeApiError({
      kind: 'validation', code: 'VALIDATION_FAILED', title: 'Invalid',
      fieldErrors: { title: ['Title is required.'], dueDate: ['Due date cannot be in the past.'] },
    }).description

    expect(description).toBe('Title is required. Due date cannot be in the past.')
  })
})
