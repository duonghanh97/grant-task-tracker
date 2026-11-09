import { useRef } from 'react'

function newKey(): string {
  // randomUUID needs a secure context (https or localhost); fall back elsewhere.
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

/**
 * One Idempotency-Key per logical submission (OP-3). Retrying the same values
 * reuses the key, so the server cannot create a duplicate task; changing the
 * values, or a successful save, starts a new submission.
 */
export function useIdempotencyKey() {
  const current = useRef<{ key: string; fingerprint: string } | null>(null)

  return {
    keyFor(values: unknown): string {
      const fingerprint = JSON.stringify(values)
      if (current.current?.fingerprint !== fingerprint) {
        current.current = { key: newKey(), fingerprint }
      }
      return current.current.key
    },
    reset() {
      current.current = null
    },
  }
}
