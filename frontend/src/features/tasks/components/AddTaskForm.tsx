import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { describeApiError, toApiError, type ApiError } from '@/api/errors'
import { TASK_PRIORITIES, type GrantDetail } from '@/api/types'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'
import { useAddTask } from '../api'
import { createTaskSchema, type TaskFormInput, type TaskFormValues } from '../schema'
import { useIdempotencyKey } from '../useIdempotencyKey'

const FIELDS = ['title', 'dueDate', 'priority'] as const
const EMPTY: TaskFormInput = { title: '', dueDate: '', priority: 'Medium' }

export function AddTaskForm({ grant }: { grant: GrantDetail }) {
  const [open, setOpen] = useState(false)
  const closed = grant.status === 'Closed'

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <div className="flex items-center gap-3">
        <DialogTrigger asChild>
          <Button disabled={closed}>
            <Plus aria-hidden />
            Add Task
          </Button>
        </DialogTrigger>
        {closed && <span className="text-sm text-muted-foreground">Closed grants cannot take new tasks.</span>}
      </div>
      <DialogContent>
        {open && <AddTaskDialogBody grant={grant} onDone={() => setOpen(false)} />}
      </DialogContent>
    </Dialog>
  )
}

function AddTaskDialogBody({ grant, onDone }: { grant: GrantDetail; onDone: () => void }) {
  const schema = useMemo(() => createTaskSchema(grant.applicationDate), [grant.applicationDate])
  const form = useForm<TaskFormInput, unknown, TaskFormValues>({ resolver: zodResolver(schema), defaultValues: EMPTY })
  const addTask = useAddTask(grant.id)
  const idempotency = useIdempotencyKey()
  const [requestError, setRequestError] = useState<ApiError | null>(null)
  const { errors } = form.formState

  const onSubmit = form.handleSubmit((values) => {
    setRequestError(null)
    addTask.mutate(
      { request: values, idempotencyKey: idempotency.keyFor(values) },
      {
        onSuccess: (task) => {
          idempotency.reset()
          toast.success('Task added', { description: `"${task.title}" was added to ${grant.referenceNumber}.` })
          onDone()
        },
        onError: (error) => {
          const apiError = toApiError(error)
          if (apiError.kind === 'validation') {
            // Put each server message on its field; keep any others for the alert.
            let unmatched = false
            for (const [field, messages] of Object.entries(apiError.fieldErrors)) {
              const known = FIELDS.find((f) => f === field)
              if (known) form.setError(known, { type: 'server', message: messages.join(' ') })
              else unmatched = true
            }
            if (unmatched || Object.keys(apiError.fieldErrors).length === 0) setRequestError(apiError)
          } else {
            setRequestError(apiError)
          }
        },
      },
    )
  })

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <DialogHeader>
        <DialogTitle>Add Task</DialogTitle>
        <DialogDescription>
          New task for {grant.organisationName} ({grant.referenceNumber}).
        </DialogDescription>
      </DialogHeader>

      {requestError && <RequestErrorAlert error={requestError} />}

      <Field id="task-title" label="Task Title" error={errors.title?.message}>
        <Input id="task-title" placeholder="e.g. Verify bank details" autoComplete="off"
          aria-invalid={!!errors.title} aria-describedby={errors.title ? 'task-title-error' : undefined}
          {...form.register('title')} />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="task-due-date" label="Due Date" error={errors.dueDate?.message}>
          <Input id="task-due-date" type="date" min={grant.applicationDate}
            aria-invalid={!!errors.dueDate} aria-describedby={errors.dueDate ? 'task-due-date-error' : undefined}
            {...form.register('dueDate')} />
        </Field>
        <Field id="task-priority" label="Priority" error={errors.priority?.message}>
          <select id="task-priority"
            className={cn(
              'h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm shadow-xs',
              'focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none',
            )}
            aria-invalid={!!errors.priority}
            {...form.register('priority')}>
            {TASK_PRIORITIES.map((p) => <option key={p} value={p}>{p}</option>)}
          </select>
        </Field>
      </div>

      <DialogFooter>
        <DialogClose asChild>
          <Button type="button" variant="outline">Cancel</Button>
        </DialogClose>
        <Button type="submit" disabled={addTask.isPending}>
          {addTask.isPending ? 'Saving…' : 'Save Task'}
        </Button>
      </DialogFooter>
    </form>
  )
}

function Field({ id, label, error, children }: { id: string; label: string; error?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>
        {label} <span className="text-destructive" aria-hidden>*</span>
      </Label>
      {children}
      {error && <p id={`${id}-error`} className="text-sm text-destructive">{error}</p>}
    </div>
  )
}

function RequestErrorAlert({ error }: { error: ApiError }) {
  const { title, description } = describeApiError(error)
  return (
    <Alert variant="destructive">
      <AlertCircle aria-hidden />
      <AlertTitle>{error.kind === 'network' ? 'Task not saved: server unreachable' : title}</AlertTitle>
      <AlertDescription>
        {description}
        {error.kind === 'network' && ' Press Save Task again to retry; the task will not be duplicated.'}
      </AlertDescription>
    </Alert>
  )
}
