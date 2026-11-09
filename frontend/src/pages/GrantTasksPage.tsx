import { Navigate, useParams } from 'react-router'
import { ApiErrorAlert } from '@/components/ApiErrorAlert'
import { Skeleton } from '@/components/ui/skeleton'
import { useGrant, useGrants } from '@/features/grants/api'
import { GrantHeader } from '@/features/grants/components/GrantHeader'
import { GrantSelector } from '@/features/grants/components/GrantSelector'

/** The single screen: pick a grant, see and work on its tasks. */
export function GrantTasksPage() {
  const { grantId } = useParams()
  const grants = useGrants()
  const selectedId = grantId === undefined ? undefined : Number(grantId)

  if (selectedId !== undefined && !Number.isInteger(selectedId)) {
    return <Navigate to="/" replace />
  }
  if (selectedId === undefined && grants.data && grants.data.length > 0) {
    return <Navigate to={`/grants/${grants.data[0].id}`} replace />
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold">Grants</h1>
        <p className="text-sm text-muted-foreground">Manage grants and track their tasks</p>
      </div>
      <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
        <GrantSelector grants={grants} selectedId={selectedId} />
        <section aria-label="Selected grant" className="min-w-0 rounded-xl border bg-background p-5">
          {selectedId !== undefined ? (
            <GrantPanel grantId={selectedId} />
          ) : (
            grants.isSuccess && <p className="text-sm text-muted-foreground">There are no grants yet.</p>
          )}
        </section>
      </div>
    </div>
  )
}

function GrantPanel({ grantId }: { grantId: number }) {
  const grant = useGrant(grantId)

  if (grant.isPending) {
    return <Skeleton className="h-48 w-full" />
  }
  if (grant.isError) {
    return <ApiErrorAlert error={grant.error} />
  }
  return <GrantHeader grant={grant.data} />
}
