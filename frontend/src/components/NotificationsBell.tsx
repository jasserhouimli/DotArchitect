import { useState, useEffect } from "react"
import { Bell } from "lucide-react"
import { notifications, NOTIFICATION_KINDS, type NotificationItem } from "@/api/client"
import { subscribeToNotifications } from "@/api/realtime"
import { Button } from "@/components/ui/button"

function kindStyle(kind: number) {
  if (kind === 1) return "bg-red-100 text-red-700"
  if (kind === 2) return "bg-amber-100 text-amber-700"
  return "bg-green-100 text-green-700"
}

export function NotificationsBell() {
  const [unread, setUnread] = useState(0)
  const [items, setItems] = useState<NotificationItem[]>([])
  const [open, setOpen] = useState(false)

  // The badge always converges to server truth: every mutation reconciles
  // instead of blindly +/-1, so duplicate/delayed events can never inflate it.
  const syncCount = () => {
    notifications.unreadCount().then(setUnread).catch(() => {})
  }

  useEffect(() => {
    syncCount()
    let cancelled = false
    let unsubscribe: (() => void) | null = null
    subscribeToNotifications(
      n => {
        if (cancelled) return
        setItems(prev => (prev.some(x => x.id === n.id) ? prev : [n, ...prev]))
        syncCount()
      },
      () => {
        if (!cancelled) syncCount()
      },
    ).then(u => {
      if (cancelled) u()
      else unsubscribe = u
    }).catch(() => {})
    return () => {
      cancelled = true
      unsubscribe?.()
    }
  }, [])

  const toggle = async () => {
    const next = !open
    setOpen(next)
    if (next) {
      notifications.list().then(setItems).catch(() => {})
      syncCount()
    }
  }

  const markRead = async (id: string) => {
    setItems(prev => prev.map(x => (x.id === id ? { ...x, isRead: true } : x)))
    await notifications.markRead(id).catch(() => {})
    syncCount()
  }

  const markAll = async () => {
    setItems(prev => prev.map(x => ({ ...x, isRead: true })))
    setUnread(0)
    await notifications.markAllRead().catch(() => {})
    syncCount()
  }

  return (
    <div className="relative">
      <Button variant="ghost" size="sm" onClick={toggle} title="Notifications">
        <span className="relative inline-flex">
          <Bell className="h-4 w-4" />
          {unread > 0 && (
            <span className="absolute -top-1.5 -right-2 min-w-4 h-4 px-0.5 rounded-full bg-red-600 text-white text-[10px] leading-4 text-center">
              {unread > 99 ? "99+" : unread}
            </span>
          )}
        </span>
      </Button>
      {open && (
        <div className="absolute right-0 top-9 z-50 w-96 max-h-[32rem] overflow-y-auto rounded-lg border bg-white shadow-lg">
          <div className="flex items-center justify-between px-3 py-2 border-b">
            <span className="text-sm font-medium">Notifications</span>
            <Button variant="ghost" size="sm" className="h-6 text-xs" onClick={markAll}>Mark all read</Button>
          </div>
          {items.length === 0 && (
            <p className="px-3 py-4 text-sm text-muted-foreground">No notifications yet. Runs you subscribe to will appear here.</p>
          )}
          {items.map(n => (
            <div key={n.id} className={`px-3 py-2 border-b text-sm cursor-pointer hover:bg-accent/50 ${n.isRead ? "" : "bg-accent/30"}`} onClick={() => markRead(n.id)}>
              <div className="flex items-center gap-2">
                <span className={`text-[10px] px-1 rounded ${kindStyle(n.kind)}`}>{NOTIFICATION_KINDS[n.kind] ?? n.kind}</span>
                <span className="text-xs text-muted-foreground">{new Date(n.createdAt).toLocaleString()}</span>
              </div>
              <div className="font-medium">{n.title}</div>
              <div className="text-muted-foreground text-xs">{n.message}</div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
