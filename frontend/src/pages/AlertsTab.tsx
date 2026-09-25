import { useState, useEffect } from "react"
import { notifications, type NotificationRule } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

export function AlertsTab({ workflowId }: { workflowId: string }) {
  const [rule, setRule] = useState<NotificationRule | null>(null)
  const [loaded, setLoaded] = useState(false)
  const [success, setSuccess] = useState(false)
  const [failure, setFailure] = useState(true)
  const [threshold, setThreshold] = useState("")
  const [webhook, setWebhook] = useState("")
  const [error, setError] = useState("")
  const [saved, setSaved] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    notifications.getRule(workflowId)
      .then(r => {
        setRule(r)
        setSuccess(r.notifyOnSuccess)
        setFailure(r.notifyOnFailure)
        setThreshold(r.rejectsAbove?.toString() ?? "")
        setWebhook(r.webhookUrl ?? "")
      })
      .catch(() => setRule(null))
      .finally(() => setLoaded(true))
  }, [workflowId])

  const save = async () => {
    setError("")
    setSaved(false)
    setSaving(true)
    try {
      const thresholdNum = threshold.trim() === "" ? null : Math.max(0, parseInt(threshold, 10) || 0)
      const r = await notifications.saveRule(workflowId, {
        notifyOnSuccess: success,
        notifyOnFailure: failure,
        rejectsAbove: thresholdNum,
        webhookUrl: webhook.trim() === "" ? null : webhook.trim(),
      })
      setRule(r)
      setSaved(true)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed")
    } finally {
      setSaving(false)
    }
  }

  const remove = async () => {
    setError("")
    try {
      await notifications.deleteRule(workflowId)
      setRule(null)
      setSuccess(false)
      setFailure(true)
      setThreshold("")
      setWebhook("")
    } catch (e) {
      setError(e instanceof Error ? e.message : "Delete failed")
    }
  }

  if (!loaded) return <p className="p-4 text-sm text-muted-foreground">Loading…</p>

  return (
    <div className="flex-1 overflow-y-auto p-6">
      <div className="max-w-xl space-y-4">
        <div>
          <h3 className="font-medium">Run notifications</h3>
          <p className="text-sm text-muted-foreground">
            Get told when runs finish — in the bell inbox here, plus an optional webhook
            (Slack/Discord incoming-webhook URLs work). Evaluated when each run ends.
          </p>
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={success} onChange={e => setSuccess(e.target.checked)} />
          Notify on success
        </label>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={failure} onChange={e => setFailure(e.target.checked)} />
          Notify on failure
        </label>

        <div>
          <label className="text-xs text-muted-foreground">Reject threshold (notify when rejected rows exceed this, empty = off)</label>
          <Input value={threshold} placeholder="e.g. 100"
            onChange={e => setThreshold(e.target.value)} />
        </div>

        <div>
          <label className="text-xs text-muted-foreground">Webhook URL (optional, POSTed as JSON)</label>
          <Input value={webhook} placeholder="https://hooks.slack.com/…"
            onChange={e => setWebhook(e.target.value)} />
          <p className="text-xs text-muted-foreground mt-1">Private/loopback hosts are blocked; secrets are never logged.</p>
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}
        {saved && <p className="text-sm text-green-600">Rule saved.</p>}

        <div className="flex gap-2">
          <Button size="sm" onClick={save} disabled={saving}>{saving ? "Saving…" : "Save rule"}</Button>
          {rule && <Button size="sm" variant="ghost" onClick={remove}>Delete rule</Button>}
        </div>
      </div>
    </div>
  )
}
