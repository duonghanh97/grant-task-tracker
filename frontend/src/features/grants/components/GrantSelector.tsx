import type { UseQueryResult } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import type { GrantStatus, GrantSummary } from '@/api/types'
import { ApiErrorAlert } from '@/components/ApiErrorAlert'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { GrantStatusBadge } from './GrantStatusBadge'

type Filter = 'All' | GrantStatus
const FILTERS: Filter[] = ['All', 'Open', 'Closed']

interface GrantSelectorProps {
  grants: UseQueryResult<GrantSummary[]>
  selectedId?: number
}

export function GrantSelector({ grants, selectedId }: GrantSelectorProps) {
  const [filter, setFilter] = useState<Filter>('All')
  const all = grants.data ?? []
  const count = (f: Filter) => (f === 'All' ? all.length : all.filter((g) => g.status === f).length)
  const visible = filter === 'All' ? all : all.filter((g) => g.status === filter)

  return (
    <nav aria-label="Grants" className="space-y-3">
      <div role="group" aria-label="Filter by status" className="flex gap-2">
        {FILTERS.map((f) => (
          <button
            key={f}
            type="button"
            aria-pressed={filter === f}
            onClick={() => setFilter(f)}
            className={cn(
              'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
              filter === f ? 'border-primary bg-primary text-primary-foreground' : 'bg-background hover:bg-muted',
            )}
          >
            {f} <span className="ml-1 opacity-70">{grants.isSuccess ? count(f) : ''}</span>
          </button>
        ))}
      </div>

      {grants.isPending && <Skeleton className="h-40 w-full" />}
      {grants.isError && <ApiErrorAlert error={grants.error} />}
      {grants.isSuccess && visible.length === 0 && (
        <p className="text-sm text-muted-foreground">No {filter === 'All' ? '' : filter.toLowerCase() + ' '}grants.</p>
      )}

      <ul className="space-y-2">
        {visible.map((grant) => {
          const selected = grant.id === selectedId
          return (
            <li key={grant.id}>
              <Link
                to={`/grants/${grant.id}`}
                aria-current={selected ? 'page' : undefined}
                className={cn(
                  'flex items-start justify-between gap-2 rounded-lg border bg-background p-3 transition-colors hover:bg-muted/60',
                  selected && 'border-primary ring-1 ring-primary',
                )}
              >
                <span className="min-w-0">
                  <span className="block truncate text-sm font-semibold">{grant.organisationName}</span>
                  <span className="block text-xs text-muted-foreground">
                    {grant.referenceNumber} • {grant.pendingTaskCount} pending{' '}
                    {grant.pendingTaskCount === 1 ? 'task' : 'tasks'}
                  </span>
                </span>
                <GrantStatusBadge status={grant.status} />
              </Link>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}
