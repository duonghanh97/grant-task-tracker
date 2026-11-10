import axios from 'axios'

/** Requests go to /api on the same origin; Vite proxies them to the backend in development. */
export const apiClient = axios.create({
  baseURL: '/api',
  timeout: 15_000,
  headers: { Accept: 'application/json' },
})
