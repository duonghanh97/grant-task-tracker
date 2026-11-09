import { AlertCircle } from 'lucide-react'
import { describeApiError, toApiError } from '@/api/errors'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'

/** Shows an API error instead of failing silently. Network errors are covered by ConnectionBanner. */
export function ApiErrorAlert({ error }: { error: unknown }) {
  const { title, description } = describeApiError(toApiError(error))
  return (
    <Alert variant="destructive">
      <AlertCircle aria-hidden />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>{description}</AlertDescription>
    </Alert>
  )
}
