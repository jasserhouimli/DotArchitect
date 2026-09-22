import { useState, useEffect, useRef, useCallback } from "react"
import {
  workflows, runs, tasks, artifactUrl, RUN_STATUSES, TASK_STATUSES,
  type WorkflowDetail, type WorkflowNode, type WorkflowEdge,
  type WorkflowRun, type TaskRun, type TaskDetail, type Attempt, type WorkflowVersion,
} from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { NodeConfigForm, defaultConfig, parseConfig } from "@/components/NodeConfigForm"

const NODE_TYPES = [
  { type: "http.request", label: "HTTP Request", color: "#3b82f6" },
  { type: "data.csv.read", label: "CSV Read", color: "#10b981" },
  { type: "data.validate", label: "Validate", color: "#f59e0b" },
  { type: "data.filter", label: "Filter", color: "#8b5cf6" },
  { type: "data.transform", label: "Transform", color: "#ec4899" },
  { type: "data.aggregate", label: "Aggregate", color: "#06b6d4" },
  { type: "data.output", label: "Output", color: "#64748b" },
]

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
  const [selectedNode, setSelectedNode] = useState<string | null>(null)
  const [connectFrom, setConnectFrom] = useState<string | null>(null)
  const [validation, setValidation] = useState<{ isValid: boolean; errors: string[]; warnings: string[] } | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState("")
  const [tab, setTab] = useState<"editor" | "runs">("editor")
  const [versions, setVersions] = useState<WorkflowVersion[]>([])
  const [showVersions, setShowVersions] = useState(false)
  const [workflowRuns, setWorkflowRuns] = useState<WorkflowRun[]>([])
  const [selectedRun, setSelectedRun] = useState<WorkflowRun | null>(null)
  const [taskRuns, setTaskRuns] = useState<TaskRun[]>([])
  const [logs, setLogs] = useState<Array<{ id: string; taskRunId: string | null; message: string; level: string; timestamp: string }>>([])
  const [expandedTask, setExpandedTask] = useState<string | null>(null)
  const [taskDetail, setTaskDetail] = useState<TaskDetail | null>(null)
  const [attempts, setAttempts] = useState<Attempt[]>([])
  const canvasRef = useRef<HTMLDivElement>(null)
  const [dragging, setDragging] = useState<string | null>(null)
  const [dragOffset, setDragOffset] = useState({ x: 0, y: 0 })

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
    setSelectedNode(id)
  }

  const removeNode = (nodeId: string) => {
    setNodes(prev => prev.filter(n => n.nodeId !== nodeId))
    setEdges(prev => prev.filter(e => e.sourceNodeId !== nodeId && e.targetNodeId !== nodeId))
    if (selectedNode === nodeId) setSelectedNode(null)
  }

  const updateNodeConfig = (nodeId: string, config: Record<string, unknown>) => {
    setNodes(prev => prev.map(n => n.nodeId === nodeId ? { ...n, configJson: JSON.stringify(config) } : n))
  }

  const handleMouseDown = (nodeId: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (connectFrom) {
      if (connectFrom !== nodeId && !edges.some(ed => ed.sourceNodeId === connectFrom && ed.targetNodeId === nodeId)) {
        setEdges(prev => [...prev, { sourceNodeId: connectFrom, targetNodeId: nodeId }])
      }
      setConnectFrom(null)
    } else {
      setSelectedNode(nodeId)
      const node = nodes.find(n => n.nodeId === nodeId)
      if (node) {
        const rect = canvasRef.current!.getBoundingClientRect()
        setDragOffset({ x: e.clientX - rect.left - node.positionX, y: e.clientY - rect.top - node.positionY })
        setDragging(nodeId)
      }
    }
  }

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    if (!dragging) return
    const rect = canvasRef.current!.getBoundingClientRect()
    const x = e.clientX - rect.left - dragOffset.x
    const y = e.clientY - rect.top - dragOffset.y
    setNodes(prev => prev.map(n => n.nodeId === dragging ? { ...n, positionX: Math.max(0, x), positionY: Math.max(0, y) } : n))
  }, [dragging, dragOffset])

  const handleMouseUp = () => setDragging(null)

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
    const poll = () => {
      runs.get(selectedRun.id).then(r => { if (alive) setSelectedRun(r) }).catch(() => {})
      runs.tasks(selectedRun.id).then(t => { if (alive) setTaskRuns(t) }).catch(() => {})
      runs.logs(selectedRun.id).then(l => { if (alive) setLogs(l) }).catch(() => {})
    }
    poll()
    const id = setInterval(() => {
      runs.get(selectedRun.id).then(r => {
        if (!alive) return
        setSelectedRun(r)
        if (!isActive(r)) { clearInterval(id); loadRuns() }
      }).catch(() => {})
      runs.tasks(selectedRun.id).then(t => { if (alive) setTaskRuns(t) }).catch(() => {})
      runs.logs(selectedRun.id).then(l => { if (alive) setLogs(l) }).catch(() => {})
    }, 2000)
    return () => { alive = false; clearInterval(id) }
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
        </div>

        <div ref={canvasRef} className="flex-1 relative bg-gray-50 overflow-hidden cursor-crosshair"
          onMouseMove={handleMouseMove} onMouseUp={handleMouseUp} onClick={() => { setSelectedNode(null); setConnectFrom(null) }}>

          <svg className="absolute inset-0 w-full h-full pointer-events-none">
            {edges.map((e, i) => {
              const src = nodes.find(n => n.nodeId === e.sourceNodeId)
              const tgt = nodes.find(n => n.nodeId === e.targetNodeId)
              if (!src || !tgt) return null
              return (
                <g key={i}>
                  <line x1={src.positionX + 40} y1={src.positionY + 20} x2={tgt.positionX + 40} y2={tgt.positionY + 20}
                    stroke="#94a3b8" strokeWidth="1.5" markerEnd="url(#arrow)" />
                </g>
              )
            })}
            <defs>
              <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto">
                <path d="M 0 0 L 10 5 L 0 10 z" fill="#94a3b8" />
              </marker>
            </defs>
          </svg>

          {nodes.map(n => {
            const nt = NODE_TYPES.find(t => t.type === n.nodeType)
            const isSelected = selectedNode === n.nodeId
            const isConnectSource = connectFrom === n.nodeId
            return (
              <div key={n.nodeId}
                className={`absolute select-none cursor-grab active:cursor-grabbing ${isSelected ? 'ring-2 ring-blue-500' : ''} ${isConnectSource ? 'ring-2 ring-amber-400' : ''}`}
                style={{ left: n.positionX, top: n.positionY }}
                onMouseDown={(e) => handleMouseDown(n.nodeId, e)}>
                <div className="w-20 h-10 rounded border-2 flex items-center justify-center text-[10px] font-medium text-white shadow-sm"
                  style={{ backgroundColor: nt?.color || '#94a3b8', borderColor: nt?.color || '#94a3b8' }}>
                  {n.label || n.nodeType}
                </div>
              </div>
            )
          })}

          {connectFrom && (
            <div className="absolute bottom-4 left-1/2 -translate-x-1/2 bg-amber-100 text-amber-700 px-3 py-1 rounded text-sm">
              Click a target node to connect, or click empty space to cancel
            </div>
          )}
        </div>

        {selectedNode && (
          <div className="w-72 border-l p-4 space-y-3 overflow-y-auto">
            <h3 className="font-semibold text-sm">Node</h3>
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
                        nodeType={node.nodeType}
                        config={parseConfig(node.configJson)}
                        onChange={c => updateNodeConfig(selectedNode, c)}
                      />
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Button size="sm" variant="outline" onClick={() => { setConnectFrom(selectedNode) }}>Connect</Button>
                    <Button size="sm" variant="destructive" onClick={() => removeNode(selectedNode)}>Delete</Button>
                  </div>
                </>
              )
            })()}
          </div>
        )}
      </div>
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
