import type { RequestHandler } from 'msw'

/** Default API handlers; individual tests override them with server.use(...). */
export const handlers: RequestHandler[] = []
