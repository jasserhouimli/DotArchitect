import { useState, useEffect } from "react"
import { X } from "lucide-react"
import { notifications, NOTIFICATION_KINDS, type NotificationItem } from "@/api/client"
import { subscribeToNotifications } from "@/api/realtime"

const MAX_TOASTS = 4

function kindBorder(kind: number) {
  if (kind === 1) return "border-red-500"
  if (kind === 2) return "border-amber-500"
  return "border-green-500"
}

function ToastCard({ toast, durationMs, onDone }: {
  toast: NotificationItem
  durationMs: number
  onDone: (id: string) => void
}) {
  const [remaining, setRemaining] = useState(durationMs)
  const [paused, setPaused] = useState(false)
  const dismiss = () => onDone(toast.id)

  useEffect(() => {
    if (paused) return
    if (remaining <= 0) {
      dismiss()
      return
    }
    const t = window.setTimeout(() => setRemaining(r => r - 250), 250)
    return () => window.clearTimeout(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [paused, remaining])

  const pct = Math.max(0, Math.min(100, (remaining / durationMs) * 100))

  return (
    <div
      className={`pointer-events-auto w-80 rounded-lg border bg-white shadow-lg border-l-4 ${kindBorder(toast.kind)} overflow-hidden`}
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onClick={() => {
        notifications.markRead(toast.id).catch(() => {})
        dismiss()
      }}
    >
      <div className="px-3 pt-2 pb-1">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            <span className="text-[10px] px-1 rounded bg-accent text-accent-foreground shrink-0">
              {NOTIFICATION_KINDS[toast.kind] ?? toast.kind}
            </span>
            <span className="text-sm font-medium truncate">{toast.title}</span>
          </div>
          <button
            className="text-muted-foreground hover:text-foreground shrink-0"
            title="Dismiss"
            onClick={e => {
              e.stopPropagation()
              dismiss()
            }}
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
        <p className="text-xs text-muted-foreground mt-0.5 line-clamp-2">{toast.message}</p>
      </div>
      <div className="flex items-center gap-2 px-3 pb-2">
        <div className="h-1 flex-1 rounded bg-accent overflow-hidden">
          <div
            className={`h-full rounded ${paused ? "bg-amber-500" : "bg-green-500"}`}
            style={{ width: `${pct}%`, transition: paused ? "none" : "width 250ms linear" }}
          />
        </div>
        <span className="text-[10px] text-muted-foreground tabular-nums w-6 text-right">
          {paused ? "❚❚" : `${Math.ceil(remaining / 1000)}s`}
        </span>
      </div>
    </div>
  )
}

export function NotificationToasts({ durationMs = 6000 }: { durationMs?: number }) {
  const [toasts, setToasts] = useState<NotificationItem[]>([])

  useEffect(() => {
    let cancelled = false
    let unsubscribe: (() => void) | null = null
    subscribeToNotifications(
      n => {
        if (cancelled) return
        setToasts(prev => {
          if (prev.some(x => x.id === n.id)) return prev
          return [n, ...prev].slice(0, MAX_TOASTS)
        })
      },
      () => {},
    ).then(u => {
      if (cancelled) u()
      else unsubscribe = u
    }).catch(() => {})
    return () => {
      cancelled = true
      unsubscribe?.()
    }
  }, [])

  if (toasts.length === 0) return null

  return (
    <div className="pointer-events-none fixed top-4 right-4 z-[100] flex flex-col gap-2">
      {toasts.map(t => (
        <ToastCard
          key={t.id}
          toast={t}
          durationMs={durationMs}
          onDone={id => setToasts(prev => prev.filter(x => x.id !== id))}
        />
      ))}
    </div>
  )
}
