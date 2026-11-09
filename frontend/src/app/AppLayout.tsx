import { ClipboardList } from 'lucide-react'
import { Outlet } from 'react-router'
import { ConnectionBanner } from '@/components/ConnectionBanner'

export function AppLayout() {
  return (
    <div className="min-h-screen bg-muted/40">
      <header className="border-b bg-background">
        <div className="mx-auto flex max-w-6xl items-center gap-2 px-6 py-4">
          <ClipboardList className="size-6 text-primary" aria-hidden />
          <span className="text-lg font-semibold">Grant Task Tracker</span>
        </div>
      </header>
      <main className="mx-auto max-w-6xl space-y-4 px-6 py-6">
        <ConnectionBanner />
        <Outlet />
      </main>
    </div>
  )
}
