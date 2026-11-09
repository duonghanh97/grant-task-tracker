import type { RequestHandler } from 'msw'
import { mockApiHandlers } from './mockApi'

/** Default API handlers; individual tests override them with server.use(...). */
export const handlers: RequestHandler[] = [...mockApiHandlers]
