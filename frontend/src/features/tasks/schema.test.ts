import { describe, expect, test } from 'vitest'
import { createTaskSchema } from './schema'

const TODAY = '2026-11-08'
const schema = createTaskSchema('2026-10-01', TODAY)
const valid = { title: 'Verify bank details', dueDate: '2026-11-20', priority: 'High' }

function errorsFor(input: Record<string, unknown>, s = schema) {
  const result = s.safeParse(input)
  return result.success ? {} : Object.fromEntries(result.error.issues.map((i) => [i.path.join('.'), i.message]))
}

describe('BR-3 title', () => {
  test.each([
    ['', 'Title is required.'],
    ['   ', 'Title is required.'],
    ['a'.repeat(101), 'Title must be 100 characters or fewer.'],
  ])('%j is rejected', (title, message) => {
    expect(errorsFor({ ...valid, title })).toEqual({ title: message })
  })

  test('100 characters is accepted and the title is trimmed', () => {
    const result = schema.parse({ ...valid, title: `  ${'a'.repeat(100)}  ` })
    expect(result.title).toBe('a'.repeat(100))
  })
})

describe('BR-2 due date', () => {
  test('yesterday is rejected', () => {
    expect(errorsFor({ ...valid, dueDate: '2026-11-07' })).toEqual({ dueDate: 'Due date cannot be in the past.' })
  })

  test('today is accepted', () => {
    expect(schema.safeParse({ ...valid, dueDate: TODAY }).success).toBe(true)
  })

  test('before the application date is rejected', () => {
    const futureGrant = createTaskSchema('2026-12-01', TODAY)
    expect(errorsFor({ ...valid, dueDate: '2026-11-20' }, futureGrant)).toEqual({
      dueDate: "Due date cannot be before the grant's application date (01 Dec 2026).",
    })
  })

  test('missing is rejected', () => {
    expect(errorsFor({ ...valid, dueDate: '' })).toEqual({ dueDate: 'Due date is required.' })
  })
})

test('unknown priority is rejected', () => {
  expect(errorsFor({ ...valid, priority: 'Urgent' })).toEqual({ priority: 'Priority is required.' })
})
