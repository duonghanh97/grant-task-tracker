import { format, parseISO } from 'date-fns'

/** 'yyyy-MM-dd' → '09 Sep 2026'. */
export function formatDate(isoDate: string): string {
  return format(parseISO(isoDate), 'dd MMM yyyy')
}

/** Today's date in the browser's time zone as 'yyyy-MM-dd'. */
export function todayIso(): string {
  return format(new Date(), 'yyyy-MM-dd')
}
