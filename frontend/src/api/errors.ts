import { isAxiosError } from 'axios'
import type { ProblemDetails } from './types'

/** Every failed API call is normalised into one of these shapes. */
export type ApiError =
  /** No usable response: offline, DNS, timeout, or the backend is down behind the proxy. */
  | { kind: 'network'; message: string }
  /** 400: request data broke a rule. Field errors are keyed by JSON property name. */
  | { kind: 'validation'; code: string; title: string; detail?: string; fieldErrors: Record<string, string[]> }
  /** 409/422 etc.: the request conflicts with the current state or a business rule. */
  | { kind: 'business'; status: number; code: string; title: string; detail: string }
  /** 404: the grant or task does not exist. */
  | { kind: 'notFound'; code: string; title: string; detail: string }
  /** Anything else, e.g. a 500 from the API itself. */
  | { kind: 'unexpected'; status?: number; title: string; detail?: string; traceId?: string }

export const NETWORK_ERROR_MESSAGE = 'Cannot reach the server. Check your connection and try again.'

function isProblem(data: unknown): data is ProblemDetails & { code: string } {
  return typeof data === 'object' && data !== null && typeof (data as ProblemDetails).code === 'string'
}

export function toApiError(error: unknown): ApiError {
  if (isApiError(error)) {
    return error
  }

  if (!isAxiosError(error)) {
    return { kind: 'unexpected', title: 'Something went wrong', detail: error instanceof Error ? error.message : undefined }
  }

  const response = error.response
  if (!response) {
    return { kind: 'network', message: NETWORK_ERROR_MESSAGE }
  }

  const status = response.status
  const data: unknown = response.data

  if (!isProblem(data)) {
    // The API always answers errors with ProblemDetails. A bare 5xx comes from
    // something in between (e.g. the dev proxy) failing to reach the API.
    if (status >= 500) {
      return { kind: 'network', message: NETWORK_ERROR_MESSAGE }
    }
    return { kind: 'unexpected', status, title: `Request failed (HTTP ${status})` }
  }

  const title = data.title ?? 'Request failed'
  const detail = data.detail ?? ''

  if (status === 400) {
    return { kind: 'validation', code: data.code, title, detail: data.detail, fieldErrors: data.errors ?? {} }
  }
  if (status === 404) {
    return { kind: 'notFound', code: data.code, title, detail }
  }
  if (status >= 500) {
    return { kind: 'unexpected', status, title, detail: data.detail, traceId: data.traceId }
  }
  return { kind: 'business', status, code: data.code, title, detail }
}

export function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === 'object' &&
    value !== null &&
    'kind' in value &&
    ['network', 'validation', 'business', 'notFound', 'unexpected'].includes((value as { kind: string }).kind)
  )
}

/** A short title and description suitable for an alert or toast. */
export function describeApiError(error: ApiError): { title: string; description: string } {
  switch (error.kind) {
    case 'network':
      return { title: 'Server unreachable', description: error.message }
    case 'validation': {
      const messages = Object.values(error.fieldErrors).flat()
      return { title: error.title, description: messages.join(' ') || error.detail || 'Check the values and try again.' }
    }
    case 'business':
    case 'notFound':
      return { title: error.title, description: error.detail }
    case 'unexpected':
      return {
        title: error.title,
        description:
          (error.detail ? `${error.detail} ` : '') +
          'Please try again.' +
          (error.traceId ? ` (Reference: ${error.traceId})` : ''),
      }
  }
}
