import { z } from 'zod'
import { TASK_PRIORITIES } from '@/api/types'
import { formatDate, todayIso } from '@/lib/dates'

export const TITLE_MAX_LENGTH = 100

/**
 * Mirrors BR-2 and BR-3 for early feedback. The server stays authoritative:
 * the browser's "today" can differ from the server's business date.
 */
export function createTaskSchema(applicationDate: string, today: string = todayIso()) {
  return z.object({
    title: z
      .string()
      .trim()
      .min(1, 'Title is required.')
      .max(TITLE_MAX_LENGTH, `Title must be ${TITLE_MAX_LENGTH} characters or fewer.`),
    dueDate: z
      .string()
      .min(1, 'Due date is required.')
      .refine((d) => d === '' || d >= today, 'Due date cannot be in the past.')
      .refine(
        (d) => d === '' || d >= applicationDate,
        `Due date cannot be before the grant's application date (${formatDate(applicationDate)}).`,
      ),
    priority: z.enum(TASK_PRIORITIES, { error: 'Priority is required.' }),
  })
}

export type TaskFormInput = z.input<ReturnType<typeof createTaskSchema>>
export type TaskFormValues = z.output<ReturnType<typeof createTaskSchema>>
