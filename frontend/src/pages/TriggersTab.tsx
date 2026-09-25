import { useState, useEffect } from "react"
import { triggers, type TriggerItem } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

const CRON_PRESETS = [
  { label: "Every minute", value: "* * * * *" },
  { label: "Every 15 minutes", value: "*/15 * * * *" },
  { label: "Hourly", value: "0 * * * *" },
  { label: "Daily at 08:00", value: "0 8 * * *" },
  { label: "Weekly Monday 08:00", value: "0 8 * * 1" },
  { label: "Custom…", value: "" },
]

const TIMEZONES = ["UTC", "Europe/Berlin", "Europe/London", "America/New_York", "America/Chicago", "America/Los_Angeles", "Asia/Dubai", "Asia/Kolkata", "Asia/Singapore", "Australia/Sydney"]

function kindLabel(t: TriggerItem) {
  return t.kind === 1 ? "webhook" : "schedule"
}

export function TriggersTab({ workflowId }: { workflowId: string }) {
  const [items, setItems] = useState<TriggerItem[]>([])
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState("")
  const [showSchedule, setShowSchedule] = useState(false)
  const [showWebhook, setShowWebhook] = useState(false)
  const [name, setName] = useState("")
  const [cron, setCron] = useState("0 8 * * *")
  const [preset, setPreset] = useState("0 8 * * *")
  const [timezone, setTimezone] = useState("UTC")
  const [overlap, setOverlap] = useState(0)
  const [revealed, setRevealed] = useState<Record<string, string>>({})

  const load = () => {
    triggers.list(workflowId).then(setItems).catch(e => setError(e instanceof Error ? e.message : "Load failed")).finally(() => setLoaded(true))
  }

  useEffect(() => { load() }, [workflowId])

  const createSchedule = async () => {
    setError("")
    try {
      await triggers.createSchedule(workflowId, { name, cronExpression: cron, timezone, overlapPolicy: overlap, isEnabled: true })
      setName("")
      setShowSchedule(false)
      load()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Create failed")
    }
  }

  const createWebhook = async () => {
    setError("")
    try {
      const created = await triggers.createWebhook(workflowId, { name })
      setRevealed(prev => ({ ...prev, [created.id]: created.url }))
      setName("")
      setShowWebhook(false)
      load()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Create failed")
    }
  }

  const toggle = async (t: TriggerItem) => {
    try {
      await triggers.updateSchedule(workflowId, t.id, { isEnabled: !t.isEnabled })
      load()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Update failed")
    }
  }

  const regenerate = async (t: TriggerItem) => {
    if (!confirm("Regenerate this webhook URL? The old URL stops working immediately.")) return
    try {
      const created = await triggers.regenerateWebhook(workflowId, t.id)
      setRevealed(prev => ({ ...prev, [created.id]: created.url }))
      load()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Regenerate failed")
    }
  }

  const remove = async (t: TriggerItem) => {
    if (!confirm(`Delete trigger "${t.name}"?`)) return
    try {
      await triggers.delete(workflowId, t.id)
      load()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Delete failed")
    }
  }

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
    } catch { /* clipboard unavailable */ }
  }

  if (!loaded) return <p className="p-4 text-sm text-muted-foreground">Loading…</p>

  return (
    <div className="flex-1 overflow-y-auto p-6">
      <div className="max-w-2xl space-y-4">
        <div>
          <h3 className="font-medium">Triggers</h3>
          <p className="text-sm text-muted-foreground">
            Schedules fire on a cron timetable; webhooks fire on an HTTP POST carrying an optional JSON payload.
            Every run records which trigger started it.
          </p>
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}

        <div className="flex gap-2">
          <Button size="sm" variant="outline" onClick={() => { setShowSchedule(!showSchedule); setShowWebhook(false) }}>+ Schedule</Button>
          <Button size="sm" variant="outline" onClick={() => { setShowWebhook(!showWebhook); setShowSchedule(false) }}>+ Webhook</Button>
        </div>

        {showSchedule && (
          <div className="rounded-md border p-3 space-y-3">
            <div>
              <label className="text-xs text-muted-foreground">Name</label>
              <Input value={name} placeholder="Nightly report" onChange={e => setName(e.target.value)} />
            </div>
            <div>
              <label className="text-xs text-muted-foreground">Timetable</label>
              <select className="w-full h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                value={preset} onChange={e => { setPreset(e.target.value); if (e.target.value) setCron(e.target.value) }}>
                {CRON_PRESETS.map(p => <option key={p.label} value={p.value}>{p.label}</option>)}
              </select>
            </div>
            {preset === "" && (
              <div>
                <label className="text-xs text-muted-foreground">Cron expression (minute hour day month weekday)</label>
                <Input value={cron} placeholder="*/15 * * * *" onChange={e => setCron(e.target.value)} />
              </div>
            )}
            <div className="flex gap-2">
              <div className="flex-1">
                <label className="text-xs text-muted-foreground">Timezone</label>
                <select className="w-full h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                  value={timezone} onChange={e => setTimezone(e.target.value)}>
                  {TIMEZONES.map(tz => <option key={tz} value={tz}>{tz}</option>)}
                </select>
              </div>
              <div className="flex-1">
                <label className="text-xs text-muted-foreground">If a run is still active</label>
                <select className="w-full h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                  value={overlap} onChange={e => setOverlap(parseInt(e.target.value, 10))}>
                  <option value={0}>Skip this window</option>
                  <option value={1}>Start anyway (queue)</option>
                </select>
              </div>
            </div>
            <Button size="sm" onClick={createSchedule} disabled={!name.trim() || !cron.trim()}>Create schedule</Button>
          </div>
        )}

        {showWebhook && (
          <div className="rounded-md border p-3 space-y-3">
            <div>
              <label className="text-xs text-muted-foreground">Name</label>
              <Input value={name} placeholder="Deploy hook" onChange={e => setName(e.target.value)} />
            </div>
            <p className="text-xs text-muted-foreground">
              Creates a secret URL. Anyone holding it can start a run — no login needed.
              POST optional JSON as the run payload (max ~512 KB).
            </p>
            <Button size="sm" onClick={createWebhook} disabled={!name.trim()}>Create webhook</Button>
          </div>
        )}

        <div className="space-y-2">
          {items.length === 0 && <p className="text-sm text-muted-foreground">No triggers yet. Manual runs always work.</p>}
          {items.map(t => (
            <div key={t.id} className="rounded-md border px-3 py-2 text-sm">
              <div className="flex items-center gap-2">
                <span className={`text-[10px] px-1 rounded ${t.kind === 1 ? "bg-purple-100 text-purple-700" : "bg-blue-100 text-blue-700"}`}>
                  {kindLabel(t)}
                </span>
                <span className="font-medium">{t.name}</span>
                <span className={`text-xs ${t.isEnabled ? "text-green-600" : "text-muted-foreground"}`}>
                  {t.isEnabled ? "enabled" : "paused"}
                </span>
                <div className="flex-1" />
                {t.kind === 0 && (
                  <Button size="sm" variant="ghost" className="h-6 text-xs" onClick={() => toggle(t)}>
                    {t.isEnabled ? "Pause" : "Resume"}
                  </Button>
                )}
                {t.kind === 1 && (
                  <Button size="sm" variant="ghost" className="h-6 text-xs" onClick={() => regenerate(t)}>Regenerate URL</Button>
                )}
                <Button size="sm" variant="ghost" className="h-6 text-xs text-red-600" onClick={() => remove(t)}>Delete</Button>
              </div>
              {t.kind === 0 && (
                <div className="text-xs text-muted-foreground mt-1">
                  <span className="font-mono">{t.cronExpression}</span> · {t.timezone} ·
                  next: {t.nextRunAt ? new Date(t.nextRunAt).toLocaleString() : "—"} ·
                  last fired: {t.lastFiredAt ? new Date(t.lastFiredAt).toLocaleString() : "never"} ·
                  overlap: {t.overlapPolicy === 0 ? "skip" : "queue"}
                </div>
              )}
              {revealed[t.id] && (
                <div className="mt-2 rounded bg-amber-50 border border-amber-200 p-2">
                  <p className="text-xs text-amber-800 mb-1">Copy now — the secret is shown only once:</p>
                  <div className="flex gap-2 items-center">
                    <code className="text-xs break-all flex-1">{revealed[t.id]}</code>
                    <Button size="sm" variant="outline" className="h-6 text-xs" onClick={() => copy(revealed[t.id])}>Copy</Button>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
