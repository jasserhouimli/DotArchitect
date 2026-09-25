import { useState, useEffect, useRef, useCallback } from "react"
import {
  workflows, runs, tasks, artifactUrl, RUN_STATUSES, TASK_STATUSES,
  type WorkflowDetail, type WorkflowNode, type WorkflowEdge,
  type WorkflowRun, type TaskRun, type TaskDetail, type Attempt, type WorkflowVersion,
} from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { NodeConfigForm, defaultConfig, parseConfig } from "@/components/NodeConfigForm"
import { NotificationsBell } from "@/components/NotificationsBell"
import { AlertsTab } from "@/pages/AlertsTab"
import { subscribeToRun } from "@/api/realtime"

const NODE_TYPES = [
  { type: "data.csv.read", label: "CSV Read", color: "#10b981" },
  { type: "data.json.read", label: "JSON Read", color: "#14b8a6" },
  { type: "http.request", label: "HTTP Request", color: "#3b82f6" },
  { type: "data.validate", label: "Validate", color: "#f59e0b" },
  { type: "data.filter", label: "Filter", color: "#8b5cf6" },
  { type: "data.sort", label: "Sort", color: "#f97316" },
  { type: "data.limit", label: "Limit", color: "#78716c" },
  { type: "data.transform", label: "Transform", color: "#ec4899" },
  { type: "data.dedupe", label: "Dedupe", color: "#84cc16" },
  { type: "data.join", label: "Join", color: "#d946ef" },
  { type: "data.aggregate", label: "Aggregate", color: "#06b6d4" },
  { type: "data.profile", label: "Profile", color: "#6366f1" },
  { type: "data.output", label: "Output", color: "#64748b" },
]

const NODE_W = 144
const NODE_H = 56

interface WorkflowEditorProps {
  workflowId: string
  onBack: () => void
  onLogout: () => void
}

function taskBadge(status: number) {
  if (status === 3) return "bg-green-100 text-green-700"
  if (status === 4) return "bg-red-100 text-red-700"
  if (status === 2 || status === 1 || status === 5) return "bg-blue-100 text-blue-700"
  if (status === 6 || status === 7) return "bg-gray-100 text-gray-600"
  return "bg-gray-100 text-gray-600"
}

function runBadge(status: number) {
  if (status === 2) return "bg-green-100 text-green-700"
  if (status === 3) return "bg-red-100 text-red-700"
  return "bg-blue-100 text-blue-700"
}

export function WorkflowEditorPage({ workflowId, onBack, onLogout }: WorkflowEditorProps) {
  const [workflow, setWorkflow] = useState<WorkflowDetail | null>(null)
  const [nodes, setNodes] = useState<WorkflowNode[]>([])
  const [edges, setEdges] = useState<WorkflowEdge[]>([])
  const [selectedNodes, setSelectedNodes] = useState<string[]>([])
  const selectedNode = selectedNodes.length > 0 ? selectedNodes[selectedNodes.length - 1] : null
  const [pendingEdge, setPendingEdge] = useState<{ from: string; x: number; y: number } | null>(null)
  const [selectedEdge, setSelectedEdge] = useState<string | null>(null)
  const [validation, setValidation] = useState<{ isValid: boolean; errors: string[]; warnings: string[] } | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState("")
  const [tab, setTab] = useState<"editor" | "runs" | "alerts">("editor")
  const [versions, setVersions] = useState<WorkflowVersion[]>([])
  const [showVersions, setShowVersions] = useState(false)
  const [workflowRuns, setWorkflowRuns] = useState<WorkflowRun[]>([])
  const [selectedRun, setSelectedRun] = useState<WorkflowRun | null>(null)
  const [taskRuns, setTaskRuns] = useState<TaskRun[]>([])
  const [logs, setLogs] = useState<Array<{ id: string; taskRunId: string | null; message: string; level: string; timestamp: string }>>([])
  const [expandedTask, setExpandedTask] = useState<string | null>(null)
  const [taskDetail, setTaskDetail] = useState<TaskDetail | null>(null)
  const [attempts, setAttempts] = useState<Attempt[]>([])
  const [live, setLive] = useState(false)
  const canvasRef = useRef<HTMLDivElement>(null)
  const [draggingId, setDraggingId] = useState<string | null>(null)
  const dragRef = useRef<{ ids: { id: string; origX: number; origY: number }[]; startX: number; startY: number; moved: boolean } | null>(null)
  const [marquee, setMarquee] = useState<{ x: number; y: number; w: number; h: number } | null>(null)
  const marqueeRef = useRef<{ x0: number; y0: number; additive: boolean } | null>(null)
  const [view, setView] = useState({ x: 0, y: 0, k: 1 })
  const panRef = useRef<{ startX: number; startY: number; origX: number; origY: number } | null>(null)
  const [ctxMenu, setCtxMenu] = useState<{ sx: number; sy: number; wx: number; wy: number; nodeId: string | null } | null>(null)

  const snap = (v: number) => Math.round(v / 10) * 10

  const load = useCallback(() => workflows.get(workflowId).then(w => {
    setWorkflow(w)
    setNodes(w.nodes)
    setEdges(w.edges)
  }).catch(e => setError(e instanceof Error ? e.message : "Load failed")), [workflowId])

  useEffect(() => { load() }, [load])

  const addNode = (type: string) => {
    const id = `node-${Date.now()}`
    const newNode: WorkflowNode = {
      nodeId: id, nodeType: type, configJson: JSON.stringify(defaultConfig(type)),
      label: NODE_TYPES.find(n => n.type === type)?.label || type,
      positionX: 200 + Math.random() * 200, positionY: 100 + Math.random() * 200
    }
    setNodes(prev => [...prev, newNode])
    setSelectedNodes([id])
  }

  const removeNodes = (ids: string[]) => {
    if (ids.length === 0) return
    const gone = new Set(ids)
    setNodes(prev => prev.filter(n => !gone.has(n.nodeId)))
    setEdges(prev => prev.filter(e => !gone.has(e.sourceNodeId) && !gone.has(e.targetNodeId)))
    setSelectedNodes(prev => prev.filter(id => !gone.has(id)))
    setSelectedEdge(prev => {
      if (!prev) return prev
      const [s, t] = prev.split("|||")
      return gone.has(s) || gone.has(t) ? null : prev
    })
  }

  const removeNode = (nodeId: string) => {
    removeNodes([nodeId])
  }

  const updateNodeConfig = (nodeId: string, config: Record<string, unknown>) => {
    setNodes(prev => prev.map(n => n.nodeId === nodeId ? { ...n, configJson: JSON.stringify(config) } : n))
  }

  const isArchived = () => workflow?.status === "Archived"

  const canvasPoint = (clientX: number, clientY: number) => {
    const rect = canvasRef.current!.getBoundingClientRect()
    return { x: (clientX - rect.left - view.x) / view.k, y: (clientY - rect.top - view.y) / view.k }
  }

  const onNodeMouseDown = (nodeId: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (isArchived()) return
    const node = nodes.find(n => n.nodeId === nodeId)
    if (!node) return
    if (e.shiftKey || e.ctrlKey || e.metaKey) {
      // Toggle membership in the selection, no drag
      setSelectedNodes(prev => prev.includes(nodeId) ? prev.filter(id => id !== nodeId) : [...prev, nodeId])
      setSelectedEdge(null)
      return
    }
    // Dragging a selected node moves the whole group; otherwise select just this one
    const group = selectedNodes.includes(nodeId) ? selectedNodes : [nodeId]
    if (!selectedNodes.includes(nodeId)) {
      setSelectedNodes([nodeId])
    }
    setSelectedEdge(null)
    dragRef.current = {
      ids: group.map(id => {
        const n = nodes.find(x => x.nodeId === id)!
        return { id, origX: n.positionX, origY: n.positionY }
      }),
      startX: e.clientX, startY: e.clientY, moved: false,
    }
    setDraggingId(nodeId)
  }

  const onNodeMouseUp = (nodeId: string, e: React.MouseEvent) => {
    e.stopPropagation()
    // Releasing a rubber band over a node finalizes the marquee (canvas mouseup never fires here)
    if (marqueeRef.current) {
      finishMarquee()
      dragRef.current = null
      setDraggingId(null)
      return
    }
    // A press without movement is a click -> select only this node (settings stay open)
    if (dragRef.current && dragRef.current.ids.some(x => x.id === nodeId)) {
      if (!dragRef.current.moved) {
        if (!(e.shiftKey || e.ctrlKey || e.metaKey)) {
          setSelectedNodes([nodeId])
        }
        setSelectedEdge(null)
      } else {
        // Snap every moved node to grid on drop
        const movedIds = new Set(dragRef.current.ids.map(x => x.id))
        setNodes(prev => prev.map(n => movedIds.has(n.nodeId)
          ? { ...n, positionX: snap(n.positionX), positionY: snap(n.positionY) }
          : n))
      }
    }
    dragRef.current = null
    setDraggingId(null)
  }

  const onCanvasMouseDown = (e: React.MouseEvent) => {
    // Empty canvas or svg background (node/port presses stop propagation and never reach here)
    const t = e.target as Element
    if (e.target === e.currentTarget || t.tagName === "svg") {
      if (e.shiftKey && !isArchived()) {
        // Rubber-band selection (additive while Shift is held)
        const rect = canvasRef.current!.getBoundingClientRect()
        const sx = e.clientX - rect.left, sy = e.clientY - rect.top
        marqueeRef.current = { x0: sx, y0: sy, additive: true }
        setMarquee({ x: sx, y: sy, w: 0, h: 0 })
        return
      }
      setSelectedNodes([])
      setSelectedEdge(null)
      setPendingEdge(null)
      setCtxMenu(null)
      marqueeRef.current = null
      setMarquee(null)
      panRef.current = { startX: e.clientX, startY: e.clientY, origX: view.x, origY: view.y }
    }
  }

  const onCanvasMouseMove = (e: React.MouseEvent) => {
    if (marqueeRef.current) {
      const rect = canvasRef.current!.getBoundingClientRect()
      const cx = e.clientX - rect.left, cy = e.clientY - rect.top
      const { x0, y0 } = marqueeRef.current
      setMarquee({ x: Math.min(x0, cx), y: Math.min(y0, cy), w: Math.abs(cx - x0), h: Math.abs(cy - y0) })
      return
    }
    if (panRef.current) {
      const p = panRef.current
      setView(v => ({ ...v, x: p.origX + (e.clientX - p.startX), y: p.origY + (e.clientY - p.startY) }))
    }
    if (dragRef.current) {
      const dx = (e.clientX - dragRef.current.startX) / view.k
      const dy = (e.clientY - dragRef.current.startY) / view.k
      if (Math.abs(dx) + Math.abs(dy) > 4) dragRef.current.moved = true
      if (dragRef.current.moved) {
        const moves = new Map(dragRef.current.ids.map(x => [x.id, x]))
        setNodes(prev => prev.map(n => {
          const o = moves.get(n.nodeId)
          return o ? { ...n, positionX: Math.max(0, o.origX + dx), positionY: Math.max(0, o.origY + dy) } : n
        }))
      }
    }
    if (pendingEdge) {
      const p = canvasPoint(e.clientX, e.clientY)
      setPendingEdge(prev => prev ? { ...prev, x: p.x, y: p.y } : prev)
    }
  }

  const finishMarquee = () => {
    // Capture everything up front: state updaters run later, after refs may change
    const m = marqueeRef.current
    const mq = marquee
    marqueeRef.current = null
    setMarquee(null)
    if (!m || !mq || mq.w + mq.h < 4) return
    const x0 = (mq.x - view.x) / view.k
    const y0 = (mq.y - view.y) / view.k
    const x1 = (mq.x + mq.w - view.x) / view.k
    const y1 = (mq.y + mq.h - view.y) / view.k
    const hit = nodes
      .filter(n => n.positionX < x1 && n.positionX + NODE_W > x0 && n.positionY < y1 && n.positionY + NODE_H > y0)
      .map(n => n.nodeId)
    if (m.additive) {
      setSelectedNodes(prev => Array.from(new Set([...prev, ...hit])))
    } else {
      setSelectedNodes(hit)
    }
    setSelectedEdge(null)
  }

  const onCanvasMouseUp = () => {
    // Releasing over empty canvas cancels a pending connection
    if (pendingEdge) setPendingEdge(null)
    finishMarquee()
    panRef.current = null
    dragRef.current = null
    setDraggingId(null)
  }

  const zoomAt = (clientX: number, clientY: number, nk: number) => {
    const k = Math.min(1.75, Math.max(0.4, nk))
    const rect = canvasRef.current!.getBoundingClientRect()
    const cx = clientX - rect.left, cy = clientY - rect.top
    setView(v => {
      const wx = (cx - v.x) / v.k, wy = (cy - v.y) / v.k
      return { k, x: cx - wx * k, y: cy - wy * k }
    })
  }

  const zoomBy = (f: number) => {
    const rect = canvasRef.current!.getBoundingClientRect()
    zoomAt(rect.left + rect.width / 2, rect.top + rect.height / 2, view.k * f)
  }

  const openCanvasMenu = (e: React.MouseEvent) => {
    e.preventDefault()
    if (isArchived()) return
    const p = canvasPoint(e.clientX, e.clientY)
    setCtxMenu({
      sx: Math.min(e.clientX, window.innerWidth - 240),
      sy: Math.min(e.clientY, window.innerHeight - 340),
      wx: Math.max(0, p.x), wy: Math.max(0, p.y), nodeId: null,
    })
  }

  const openNodeMenu = (nodeId: string, e: React.MouseEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setSelectedNodes([nodeId])
    setCtxMenu({
      sx: Math.min(e.clientX, window.innerWidth - 220),
      sy: Math.min(e.clientY, window.innerHeight - 160),
      wx: 0, wy: 0, nodeId,
    })
  }

  const addNodeAt = (type: string, x: number, y: number) => {
    const id = `node-${Date.now()}`
    setNodes(prev => [...prev, {
      nodeId: id, nodeType: type, configJson: JSON.stringify(defaultConfig(type)),
      label: NODE_TYPES.find(n => n.type === type)?.label || type,
      positionX: snap(Math.max(0, x - NODE_W / 2)), positionY: snap(Math.max(0, y - NODE_H / 2)),
    }])
    setSelectedNodes([id])
  }

  const duplicateNode = (nodeId: string) => {
    duplicateNodes([nodeId])
  }

  const duplicateNodes = (ids: string[]) => {
    if (ids.length === 0 || isArchived()) return
    const base = Date.now()
    const copies = nodes
      .filter(n => ids.includes(n.nodeId))
      .map((src, i) => ({
        ...src,
        nodeId: `node-${base}-${i}`,
        label: `${src.label || src.nodeType} copy`,
        positionX: snap(src.positionX + 24),
        positionY: snap(src.positionY + 24),
      }))
    if (copies.length === 0) return
    setNodes(prev => [...prev, ...copies])
    setSelectedNodes(copies.map(c => c.nodeId))
  }

  const startConnection = (nodeId: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (isArchived()) return
    const p = canvasPoint(e.clientX, e.clientY)
    setPendingEdge({ from: nodeId, x: p.x, y: p.y })
  }

  const finishConnection = (nodeId: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (marqueeRef.current) {
      finishMarquee()
      return
    }
    if (pendingEdge && pendingEdge.from !== nodeId) {
      const from = pendingEdge.from
      if (!edges.some(ed => ed.sourceNodeId === from && ed.targetNodeId === nodeId)) {
        setEdges(prev => [...prev, { sourceNodeId: from, targetNodeId: nodeId }])
      }
    }
    setPendingEdge(null)
    dragRef.current = null
    setDraggingId(null)
  }

  const edgeKey = (source: string, target: string) => `${source}|||${target}`

  const deleteSelectedEdge = () => {
    if (!selectedEdge) return
    const [s, t] = selectedEdge.split("|||")
    setEdges(prev => prev.filter(x => !(x.sourceNodeId === s && x.targetNodeId === t)))
    setSelectedEdge(null)
  }

  useEffect(() => {
    if (tab !== "editor") return
    const onKey = (e: KeyboardEvent) => {
      const el = document.activeElement
      if (el && (el.tagName === "INPUT" || el.tagName === "TEXTAREA" || el.tagName === "SELECT")) return
      if (e.key === "Escape") {
        setPendingEdge(null)
        setSelectedEdge(null)
        setSelectedNodes([])
        setCtxMenu(null)
        marqueeRef.current = null
        setMarquee(null)
      }
      if ((e.key === "Delete" || e.key === "Backspace") && !isArchived()) {
        if (selectedEdge) {
          deleteSelectedEdge()
        } else if (selectedNodes.length > 0) {
          removeNodes(selectedNodes)
        }
      }
    }
    window.addEventListener("keydown", onKey)
    return () => window.removeEventListener("keydown", onKey)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, selectedEdge, selectedNodes])

  const save = async () => {
    setSaving(true)
    setError("")
    try {
      await workflows.update(workflowId, { nodes, edges })
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed")
    } finally {
      setSaving(false)
    }
  }

  const publish = async () => {
    await save()
    try {
      const result = await workflows.publish(workflowId)
      alert(`Published as version ${result.version}`)
      load()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Publish failed")
    }
  }

  const validate = async () => {
    await save()
    try {
      const result = await workflows.validate(workflowId)
      setValidation(result)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Validate failed")
    }
  }

  const archive = async () => {
    if (!confirm("Archive this workflow? It can no longer be edited or run.")) return
    try {
      await workflows.archive(workflowId)
      load()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Archive failed")
    }
  }

  const toggleVersions = async () => {
    if (!showVersions) {
      try {
        setVersions(await workflows.versions(workflowId))
      } catch (e) {
        setError(e instanceof Error ? e.message : "Failed to load versions")
        return
      }
    }
    setShowVersions(!showVersions)
  }

  const isActive = (run: WorkflowRun) => run.status === 0 || run.status === 1

  const loadRuns = useCallback(() => runs.list(workflowId).then(setWorkflowRuns).catch(() => {}), [workflowId])
  useEffect(() => { if (tab === "runs") loadRuns() }, [tab, loadRuns])

  useEffect(() => {
    if (!selectedRun) return
    let alive = true
    let unsubscribe: (() => void) | null = null

    const applyTask = (t: TaskRun) => {
      if (!alive) return
      setTaskRuns(prev => {
        const i = prev.findIndex(x => x.id === t.id)
        if (i < 0) return [...prev, t]
        if (prev[i] === t) return prev
        const next = [...prev]
        next[i] = t
        return next
      })
    }
    const applyLog = (l: { id: string; taskRunId: string | null; message: string; level: string; timestamp: string }) => {
      if (!alive) return
      setLogs(prev => (prev.some(x => x.id === l.id) ? prev : [...prev, l]))
    }

    // Snapshot first, then join for live updates, then refetch to close the gap.
    const boot = async () => {
      const id = selectedRun.id
      try {
        const [r, t, l] = await Promise.all([runs.get(id), runs.tasks(id), runs.logs(id)])
        if (!alive) return
        setSelectedRun(r)
        setTaskRuns(t)
        setLogs(l)
      } catch { /* run may be gone; live events will correct */ }
      if (!alive) return
      try {
        unsubscribe = await subscribeToRun(id, {
          onRun: r => {
            if (!alive) return
            setSelectedRun(r)
            setWorkflowRuns(prev => prev.map(x => (x.id === r.id ? r : x)))
            if (!isActive(r)) loadRuns()
          },
          onTask: applyTask,
          onLog: applyLog,
          onReconnect: () => {
            if (!alive) return
            runs.get(id).then(setSelectedRun).catch(() => {})
            runs.tasks(id).then(setTaskRuns).catch(() => {})
            runs.logs(id).then(setLogs).catch(() => {})
          },
        })
        if (alive) setLive(true)
      } catch {
        // Hub unreachable: fall back to one snapshot; user can switch runs to retry.
        if (alive) {
          setLive(false)
          loadRuns()
        }
      }
    }
    boot()

    return () => {
      alive = false
      setLive(false)
      unsubscribe?.()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedRun?.id])

  useEffect(() => {
    if (!expandedTask) { setTaskDetail(null); setAttempts([]); return }
    tasks.get(expandedTask).then(setTaskDetail).catch(() => {})
    tasks.attempts(expandedTask).then(setAttempts).catch(() => {})
  }, [expandedTask, taskRuns])

  const handleRun = async () => {
    setError("")
    try {
      const runId = await runs.start(workflowId)
      setTab("runs")
      const run = await runs.get(runId)
      setSelectedRun(run)
      loadRuns()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Run failed to start")
    }
  }

  const handleCancel = async () => {
    if (!selectedRun) return
    try {
      await runs.cancel(selectedRun.id)
      const run = await runs.get(selectedRun.id)
      setSelectedRun(run)
      loadRuns()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Cancel failed")
    }
  }

  const handleRetry = async (taskId: string) => {
    try {
      await tasks.retry(taskId)
      if (selectedRun) {
        runs.tasks(selectedRun.id).then(setTaskRuns).catch(() => {})
        runs.get(selectedRun.id).then(setSelectedRun).catch(() => {})
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Retry failed")
    }
  }

  if (!workflow) return <p className="p-8">Loading...</p>

  const archived = workflow.status === "Archived"

  return (
    <div className="h-screen flex flex-col">
      <div className="flex items-center justify-between px-4 py-2 border-b">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={onBack}>Back</Button>
          <h1 className="font-semibold">{workflow.name}</h1>
          <span className={`text-xs px-2 py-0.5 rounded ${workflow.status === 'Published' ? 'bg-green-100 text-green-700' : workflow.status === 'Archived' ? 'bg-gray-100 text-gray-600' : 'bg-blue-100 text-blue-700'}`}>
            {workflow.status} v{workflow.currentVersion}
          </span>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={toggleVersions}>Versions</Button>
          <Button variant="outline" size="sm" onClick={validate} disabled={archived}>Validate</Button>
          <Button variant="outline" size="sm" onClick={save} disabled={saving || archived}>{saving ? "Saving..." : "Save"}</Button>
          <Button size="sm" onClick={publish} disabled={archived}>Publish</Button>
          <Button size="sm" variant="outline" onClick={handleRun} disabled={archived}>Run</Button>
          <Button size="sm" variant="ghost" onClick={archive} disabled={archived}>Archive</Button>
          <NotificationsBell />
          <Button variant="ghost" size="sm" onClick={onLogout}>Logout</Button>
        </div>
      </div>

      {error && <div className="px-4 py-2 text-sm bg-red-50 text-red-700">{error}</div>}

      {validation && (
        <div className={`px-4 py-2 text-sm ${validation.isValid ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
          {validation.isValid ? "Workflow is valid!" : validation.errors.join("; ")}
          {validation.warnings.length > 0 && <span className="text-amber-600 ml-2">Warnings: {validation.warnings.join("; ")}</span>}
        </div>
      )}

      {showVersions && (
        <div className="px-4 py-2 text-sm border-b bg-muted/30">
          <span className="font-medium">Published versions: </span>
          {versions.length === 0 ? <span className="text-muted-foreground">none yet</span> : versions.map(v => (
            <span key={v.id} className="inline-block mr-2 px-2 py-0.5 border rounded bg-background">
              v{v.versionNumber} · {new Date(v.publishedAt).toLocaleString()}
            </span>
          ))}
        </div>
      )}

      <div className="flex gap-1 px-4 border-b">
        <button className={`px-3 py-1 text-sm ${tab === "editor" ? "border-b-2 border-primary font-medium" : "text-muted-foreground"}`} onClick={() => setTab("editor")}>Editor</button>
        <button className={`px-3 py-1 text-sm ${tab === "runs" ? "border-b-2 border-primary font-medium" : "text-muted-foreground"}`} onClick={() => setTab("runs")}>Runs ({workflowRuns.length})</button>
        <button className={`px-3 py-1 text-sm ${tab === "alerts" ? "border-b-2 border-primary font-medium" : "text-muted-foreground"}`} onClick={() => setTab("alerts")}>Alerts</button>
      </div>

      {tab === "editor" ? (
      <div className="flex flex-1 overflow-hidden">
        <div className="w-48 border-r p-2 space-y-1 overflow-y-auto">
          <p className="text-xs font-medium text-muted-foreground mb-2 px-2">Node Types</p>
          {NODE_TYPES.map(nt => (
            <button key={nt.type} disabled={archived} className="w-full text-left px-2 py-1.5 text-sm rounded hover:bg-accent transition-colors disabled:opacity-50"
              onClick={() => addNode(nt.type)}>
              <span className="inline-block w-2 h-2 rounded-full mr-2" style={{ backgroundColor: nt.color }} />
              {nt.label}
            </button>
          ))}
          <p className="text-[11px] text-muted-foreground px-2 pt-2 leading-relaxed">
            Right-click canvas to add here · drag empty space to pan · scroll to zoom · Shift+click or Shift+drag to select many · Del removes selection
          </p>
        </div>

        <div ref={canvasRef} className="flex-1 relative bg-gray-50 overflow-hidden"
          style={{ backgroundImage: "radial-gradient(#d1d5db 1px, transparent 1px)", backgroundSize: "20px 20px" }}
          onMouseDown={onCanvasMouseDown} onMouseMove={onCanvasMouseMove} onMouseUp={onCanvasMouseUp}
          onWheel={(e) => zoomAt(e.clientX, e.clientY, view.k * Math.exp(-e.deltaY * 0.0015))}
          onContextMenu={openCanvasMenu}>

          <div className="absolute left-0 top-0 origin-top-left" style={{ transform: `translate(${view.x}px, ${view.y}px) scale(${view.k})`, width: 0, height: 0 }}>
          <svg width={5000} height={5000} className="absolute left-0 top-0 overflow-visible" onMouseDown={() => { setSelectedNodes([]); setSelectedEdge(null); setPendingEdge(null) }}>
            <defs>
              <marker id="reflow-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M 0 0 L 10 5 L 0 10 z" fill="#64748b" />
              </marker>
              <marker id="reflow-arrow-sel" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M 0 0 L 10 5 L 0 10 z" fill="#0284c7" />
              </marker>
              <marker id="reflow-arrow-pending" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M 0 0 L 10 5 L 0 10 z" fill="#d97706" />
              </marker>
            </defs>
            {edges.map((e) => {
              const src = nodes.find(n => n.nodeId === e.sourceNodeId)
              const tgt = nodes.find(n => n.nodeId === e.targetNodeId)
              if (!src || !tgt) return null
              const sx = src.positionX + NODE_W, sy = src.positionY + NODE_H / 2
              const tx = tgt.positionX, ty = tgt.positionY + NODE_H / 2
              const dx = Math.max(32, Math.abs(tx - sx) / 2)
              const d = `M ${sx} ${sy} C ${sx + dx} ${sy}, ${tx - dx} ${ty}, ${tx} ${ty}`
              const key = edgeKey(e.sourceNodeId, e.targetNodeId)
              const sel = selectedEdge === key
              return (
                <g key={key}>
                  <path d={d} fill="none" stroke={sel ? "#0284c7" : "#64748b"} strokeWidth={sel ? 2.5 : 2}
                    markerEnd={`url(#${sel ? "reflow-arrow-sel" : "reflow-arrow"})`} />
                  <path d={d} fill="none" stroke="transparent" strokeWidth={16} className="cursor-pointer"
                    onClick={(ev) => { ev.stopPropagation(); setSelectedEdge(sel ? null : key); setSelectedNodes([]) }} />
                </g>
              )
            })}
            {pendingEdge && (() => {
              const src = nodes.find(n => n.nodeId === pendingEdge.from)
              if (!src) return null
              const sx = src.positionX + NODE_W, sy = src.positionY + NODE_H / 2
              const dx = Math.max(32, Math.abs(pendingEdge.x - sx) / 2)
              const d = `M ${sx} ${sy} C ${sx + dx} ${sy}, ${pendingEdge.x - dx} ${pendingEdge.y}, ${pendingEdge.x} ${pendingEdge.y}`
              return <path d={d} fill="none" stroke="#d97706" strokeWidth={2} strokeDasharray="6 4" markerEnd="url(#reflow-arrow-pending)" />
            })()}
          </svg>

          {nodes.map(n => {
            const nt = NODE_TYPES.find(t => t.type === n.nodeType)
            const isSelected = selectedNodes.includes(n.nodeId)
            return (
              <div key={n.nodeId}
                className={`absolute select-none ${draggingId === n.nodeId ? "cursor-grabbing z-20" : "cursor-grab"} ${isSelected ? "z-10" : ""}`}
                style={{ left: n.positionX, top: n.positionY }}
                onMouseDown={(e) => onNodeMouseDown(n.nodeId, e)}
                onMouseUp={(e) => onNodeMouseUp(n.nodeId, e)}
                onContextMenu={(e) => openNodeMenu(n.nodeId, e)}>
                <div className="rounded-lg border-2 bg-white shadow-sm hover:shadow-md transition-shadow"
                  style={{ width: NODE_W, height: NODE_H, borderColor: isSelected ? "#3b82f6" : (nt?.color || "#94a3b8") }}>
                  <div className="flex items-center gap-1.5 px-2 pt-1.5">
                    <span className="inline-block w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: nt?.color || "#94a3b8" }} />
                    <span className="text-xs font-medium truncate">{n.label || n.nodeType}</span>
                  </div>
                  <div className="px-2 pb-1.5 pl-5 text-[10px] font-mono text-muted-foreground truncate">{n.nodeType}</div>
                </div>
                <div title="Input — drop a connection here"
                  className="absolute w-3.5 h-3.5 rounded-full border-2 border-white bg-slate-400 shadow cursor-crosshair hover:bg-slate-600 hover:scale-110 transition-transform"
                  style={{ left: -7, top: NODE_H / 2 - 7 }}
                  onMouseDown={(e) => { e.stopPropagation(); if (pendingEdge) finishConnection(n.nodeId, e) }}
                  onMouseUp={(e) => finishConnection(n.nodeId, e)} />
                <div title="Output — drag to another node's input to connect"
                  className="absolute w-3.5 h-3.5 rounded-full border-2 border-white shadow cursor-crosshair hover:scale-125 transition-transform"
                  style={{ right: -7, top: NODE_H / 2 - 7, backgroundColor: nt?.color || "#94a3b8" }}
                  onMouseDown={(e) => startConnection(n.nodeId, e)} />
              </div>
            )
          })}

          {pendingEdge && (
            <div className="absolute bottom-4 left-1/2 -translate-x-1/2 bg-amber-100 text-amber-800 px-3 py-1 rounded text-sm shadow">
              Drop on another node's <b>left port</b> to connect — click empty space or press Esc to cancel
            </div>
          )}
          {selectedEdge && !archived && (() => {
            const e = edges.find(x => edgeKey(x.sourceNodeId, x.targetNodeId) === selectedEdge)
            const src = e && nodes.find(n => n.nodeId === e.sourceNodeId)
            const tgt = e && nodes.find(n => n.nodeId === e.targetNodeId)
            if (!e || !src || !tgt) return null
            const p0x = src.positionX + NODE_W, p0y = src.positionY + NODE_H / 2
            const p3x = tgt.positionX, p3y = tgt.positionY + NODE_H / 2
            const dx = Math.max(32, Math.abs(p3x - p0x) / 2)
            const mx = (p0x + 3 * (p0x + dx) + 3 * (p3x - dx) + p3x) / 8
            const my = (p0y + 3 * p0y + 3 * p3y + p3y) / 8
            return (
              <button title="Delete connection"
                className="absolute z-30 w-5 h-5 rounded-full bg-white border shadow text-[11px] leading-none text-red-600 hover:bg-red-50"
                style={{ left: mx, top: my, transform: "translate(-50%, -50%)" }}
                onMouseDown={(ev) => ev.stopPropagation()}
                onClick={(ev) => { ev.stopPropagation(); deleteSelectedEdge() }}>×</button>
            )
          })()}
          </div>

          {marquee && (
            <div className="absolute z-30 border-2 border-blue-500 bg-blue-500/10 rounded-sm pointer-events-none"
              style={{ left: marquee.x, top: marquee.y, width: marquee.w, height: marquee.h }} />
          )}

          <div className="absolute top-2 right-2 z-20 flex items-center gap-1 bg-white border rounded shadow px-1 py-0.5 text-xs">
            <button className="px-1.5 hover:bg-accent rounded" onClick={() => zoomBy(1 / 1.2)} title="Zoom out">−</button>
            <span className="w-10 text-center text-muted-foreground">{Math.round(view.k * 100)}%</span>
            <button className="px-1.5 hover:bg-accent rounded" onClick={() => zoomBy(1.2)} title="Zoom in">+</button>
            <button className="px-1.5 hover:bg-accent rounded text-muted-foreground" onClick={() => setView({ x: 0, y: 0, k: 1 })} title="Reset view">Reset</button>
          </div>
        </div>

        {ctxMenu && (
          <div className="fixed z-50 w-60 rounded-lg border bg-white shadow-lg py-1 text-sm"
            style={{ left: ctxMenu.sx, top: ctxMenu.sy }}
            onMouseDown={(e) => e.stopPropagation()}>
            {ctxMenu.nodeId ? (
              <>
                <div className="px-3 py-1 text-xs font-medium text-muted-foreground">Node actions</div>
                <button className="flex w-full items-center px-3 py-1.5 text-left hover:bg-accent"
                  onClick={() => { duplicateNode(ctxMenu.nodeId!); setCtxMenu(null) }}>
                  ⧉ Duplicate node
                </button>
                <button className="flex w-full items-center px-3 py-1.5 text-left text-red-600 hover:bg-accent"
                  onClick={() => { removeNode(ctxMenu.nodeId!); setCtxMenu(null) }}>
                  × Delete node
                </button>
              </>
            ) : (
              <>
                <div className="px-3 py-1 text-xs font-medium text-muted-foreground">Add node here</div>
                {NODE_TYPES.map(nt => (
                  <button key={nt.type} className="flex w-full items-center gap-2 px-3 py-1.5 text-left hover:bg-accent"
                    onClick={() => { addNodeAt(nt.type, ctxMenu.wx, ctxMenu.wy); setCtxMenu(null) }}>
                    <span className="inline-block w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: nt.color }} />
                    <span>{nt.label}</span>
                    <span className="ml-auto text-[10px] font-mono text-muted-foreground">{nt.type.split(".").pop()}</span>
                  </button>
                ))}
              </>
            )}
          </div>
        )}

        {selectedNodes.length > 1 ? (
          <div className="w-72 border-l p-4 space-y-3 overflow-y-auto">
            <div className="flex items-center justify-between">
              <h3 className="font-semibold text-sm">{selectedNodes.length} nodes selected</h3>
              <Button variant="ghost" size="sm" className="h-6 w-6 p-0" onClick={() => setSelectedNodes([])}>×</Button>
            </div>
            <div className="text-xs text-muted-foreground space-y-1">
              {selectedNodes.map(id => {
                const n = nodes.find(x => x.nodeId === id)
                return <div key={id} className="truncate">· {n?.label || n?.nodeType || id}</div>
              })}
            </div>
            <p className="text-xs text-muted-foreground">Drag any of them to move the group. Settings are edited one node at a time — click a single node.</p>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" onClick={() => duplicateNodes(selectedNodes)} disabled={archived}>Duplicate</Button>
              <Button size="sm" variant="destructive" onClick={() => removeNodes(selectedNodes)} disabled={archived}>Delete all</Button>
            </div>
          </div>
        ) : selectedNode && (
          <div className="w-72 border-l p-4 space-y-3 overflow-y-auto">
            <div className="flex items-center justify-between">
              <h3 className="font-semibold text-sm">Node settings</h3>
              <Button variant="ghost" size="sm" className="h-6 w-6 p-0" onClick={() => setSelectedNodes([])}>×</Button>
            </div>
            {(() => {
              const node = nodes.find(n => n.nodeId === selectedNode)
              if (!node) return null
              return (
                <>
                  <div>
                    <label className="text-xs text-muted-foreground">Label</label>
                    <Input value={node.label || ""} onChange={e => setNodes(prev => prev.map(n => n.nodeId === selectedNode ? { ...n, label: e.target.value } : n))} />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Type</label>
                    <p className="text-sm font-mono">{node.nodeType}</p>
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground font-medium">Configuration</label>
                    <div className="mt-1">
                      <NodeConfigForm
                        key={selectedNode}
                        nodeType={node.nodeType}
                        config={parseConfig(node.configJson)}
                        workflowId={workflowId}
                        onChange={c => updateNodeConfig(selectedNode, c)}
                      />
                    </div>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    To connect: drag from this node's <b>right port</b> ● to another node's left port.
                  </p>
                  <div className="flex gap-2">
                    <Button size="sm" variant="destructive" onClick={() => removeNode(selectedNode)} disabled={archived}>Delete node</Button>
                  </div>
                </>
              )
            })()}
          </div>
        )}
      </div>
      ) : tab === "alerts" ? (
        <AlertsTab workflowId={workflowId} />
      ) : (
      <div className="flex flex-1 overflow-hidden">
        <div className="w-80 border-r p-3 space-y-2 overflow-y-auto">
          <h3 className="font-medium text-sm">Runs</h3>
          {workflowRuns.map(r => (
            <div key={r.id} className={`p-2 border rounded cursor-pointer text-sm ${selectedRun?.id === r.id ? 'bg-accent' : 'hover:bg-accent/50'}`} onClick={() => { setSelectedRun(r); setExpandedTask(null) }}>
              <div className="flex justify-between">
                <span className="font-mono text-xs">{r.id.slice(0, 8)}</span>
                <span className={`text-xs px-1 rounded ${runBadge(r.status)}`}>{RUN_STATUSES[r.status]}</span>
              </div>
              <div className="text-xs text-muted-foreground">v{r.versionNumber} · {new Date(r.createdAt).toLocaleString()}</div>
              <div className="text-xs text-muted-foreground">{r.completedTasks}/{r.totalTasks} tasks done{r.failedTasks > 0 ? ` · ${r.failedTasks} failed` : ""}</div>
              {r.error && <div className="text-xs text-red-600 truncate">{r.error}</div>}
            </div>
          ))}
          {workflowRuns.length === 0 && <p className="text-sm text-muted-foreground">No runs yet. Click Run.</p>}
        </div>
        <div className="flex-1 p-4 space-y-4 overflow-y-auto">
          {!selectedRun ? <p className="text-sm text-muted-foreground">Select a run</p> : (
            <>
              <div className="flex items-center gap-2">
                <h3 className="font-medium">Run {selectedRun.id.slice(0, 8)}</h3>
                <span className="text-xs text-muted-foreground">v{selectedRun.versionNumber} · {RUN_STATUSES[selectedRun.status]} · {selectedRun.completedTasks}/{selectedRun.totalTasks} done</span>
                <span className={`flex items-center gap-1 text-xs ${live ? "text-green-600" : "text-muted-foreground"}`} title={live ? "Live updates connected" : "Connecting live updates…"}>
                  <span className={`inline-block w-1.5 h-1.5 rounded-full ${live ? "bg-green-500" : "bg-gray-300"}`} />{live ? "Live" : "…"}
                </span>
                <div className="flex-1" />
                {isActive(selectedRun) && <Button size="sm" variant="destructive" onClick={handleCancel}>Cancel run</Button>}
              </div>
              {selectedRun.error && <div className="text-sm text-red-600 bg-red-50 rounded px-2 py-1">{selectedRun.error}</div>}
              <div className="space-y-1">
                {taskRuns.map(t => (
                  <div key={t.id} className="border rounded px-2 py-1">
                    <div className="flex justify-between items-center text-sm cursor-pointer" onClick={() => setExpandedTask(expandedTask === t.id ? null : t.id)}>
                      <span>{t.nodeId} <span className="text-muted-foreground">({t.nodeType})</span>
                        {t.outputSummary && <span className="text-muted-foreground text-xs ml-2">{t.outputSummary}</span>}
                      </span>
                      <span className="flex items-center gap-2">
                        <span className="text-xs text-muted-foreground">×{t.attemptCount}</span>
                        <span className={`text-xs px-1 rounded ${taskBadge(t.status)}`}>{TASK_STATUSES[t.status]}</span>
                      </span>
                    </div>
                    {t.error && <div className="text-xs text-red-600 mt-1">{t.error}</div>}
                    {expandedTask === t.id && (
                      <div className="mt-2 space-y-2 border-t pt-2">
                        <div className="flex gap-2">
                          {t.status === 4 && <Button size="sm" variant="outline" onClick={() => handleRetry(t.id)}>Retry task</Button>}
                          {t.nodeType === "data.output" && t.status === 3 && (
                            <a className="text-xs underline text-blue-600 self-center" href={artifactUrl(selectedRun.id, t.nodeId)} target="_blank" rel="noreferrer">
                              Download artifact
                            </a>
                          )}
                        </div>
                        {taskDetail && taskDetail.id === t.id && taskDetail.outputJson && (
                          <details className="text-xs">
                            <summary className="cursor-pointer text-muted-foreground">Result data preview</summary>
                            <pre className="bg-gray-900 text-gray-100 p-2 rounded mt-1 max-h-48 overflow-auto">
                              {taskDetail.outputJson.slice(0, 2000)}{taskDetail.outputJson.length > 2000 ? "…" : ""}
                            </pre>
                          </details>
                        )}
                        <div>
                          <h5 className="text-xs font-medium mb-1">Attempts ({attempts.length})</h5>
                          {attempts.map(a => (
                            <div key={a.id} className="text-xs border rounded px-2 py-1 mb-1">
                              <div className="flex justify-between">
                                <span>#{a.attemptNumber} · {TASK_STATUSES[a.status] ?? a.status}</span>
                                <span className="text-muted-foreground">{new Date(a.startedAt).toLocaleTimeString()}</span>
                              </div>
                              {a.error && <div className="text-red-600">{a.error}</div>}
                              {a.log && <div className="text-muted-foreground">{a.log}</div>}
                            </div>
                          ))}
                          {attempts.length === 0 && <p className="text-xs text-muted-foreground">No attempts yet.</p>}
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
              <div>
                <h4 className="text-sm font-medium mb-1">Logs</h4>
                <div className="bg-gray-900 text-gray-100 p-2 rounded text-xs font-mono max-h-60 overflow-y-auto">
                  {logs.map((l) => (
                    <div key={l.id} className={l.level === 'Error' ? 'text-red-400' : l.level === 'Warning' ? 'text-amber-300' : ''}>
                      [{new Date(l.timestamp).toLocaleTimeString()}] {l.message}
                    </div>
                  ))}
                  {logs.length === 0 && <div className="text-gray-500">No logs</div>}
                </div>
              </div>
            </>
          )}
        </div>
      </div>
      )}
    </div>
  )
}
