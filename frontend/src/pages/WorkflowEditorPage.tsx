import { useState, useEffect, useRef, useCallback } from "react"
import { workflows, type WorkflowDetail, type WorkflowNode, type WorkflowEdge } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"


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

export function WorkflowEditorPage({ workflowId, onBack, onLogout }: WorkflowEditorProps) {
  const [workflow, setWorkflow] = useState<WorkflowDetail | null>(null)
  const [nodes, setNodes] = useState<WorkflowNode[]>([])
  const [edges, setEdges] = useState<WorkflowEdge[]>([])
  const [selectedNode, setSelectedNode] = useState<string | null>(null)
  const [connectFrom, setConnectFrom] = useState<string | null>(null)
  const [validation, setValidation] = useState<{ isValid: boolean; errors: string[]; warnings: string[] } | null>(null)
  const [saving, setSaving] = useState(false)
  const canvasRef = useRef<HTMLDivElement>(null)
  const [dragging, setDragging] = useState<string | null>(null)
  const [dragOffset, setDragOffset] = useState({ x: 0, y: 0 })

  const load = () => workflows.get(workflowId).then(w => {
    setWorkflow(w)
    setNodes(w.nodes)
    setEdges(w.edges)
  })

  useEffect(() => { load() }, [workflowId])

  const addNode = (type: string) => {
    const id = `node-${Date.now()}`
    const newNode: WorkflowNode = {
      nodeId: id, nodeType: type, configJson: "{}",
      label: NODE_TYPES.find(n => n.type === type)?.label || type,
      positionX: 200 + Math.random() * 200, positionY: 100 + Math.random() * 200
    }
    setNodes(prev => [...prev, newNode])
  }

  const removeNode = (nodeId: string) => {
    setNodes(prev => prev.filter(n => n.nodeId !== nodeId))
    setEdges(prev => prev.filter(e => e.sourceNodeId !== nodeId && e.targetNodeId !== nodeId))
    if (selectedNode === nodeId) setSelectedNode(null)
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
    await workflows.update(workflowId, { nodes, edges })
    setSaving(false)
  }

  const publish = async () => {
    await save()
    const result = await workflows.publish(workflowId)
    alert(`Published as version ${result.version}`)
    load()
  }

  const validate = async () => {
    await save()
    const result = await workflows.validate(workflowId)
    setValidation(result)
  }

  if (!workflow) return <p className="p-8">Loading...</p>

  return (
    <div className="h-screen flex flex-col">
      <div className="flex items-center justify-between px-4 py-2 border-b">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={onBack}>Back</Button>
          <h1 className="font-semibold">{workflow.name}</h1>
          <span className={`text-xs px-2 py-0.5 rounded ${workflow.status === 'Published' ? 'bg-green-100 text-green-700' : 'bg-blue-100 text-blue-700'}`}>
            {workflow.status} v{workflow.currentVersion}
          </span>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={validate}>Validate</Button>
          <Button variant="outline" size="sm" onClick={save} disabled={saving}>{saving ? "Saving..." : "Save"}</Button>
          <Button size="sm" onClick={publish}>Publish</Button>
          <Button variant="ghost" size="sm" onClick={onLogout}>Logout</Button>
        </div>
      </div>

      {validation && (
        <div className={`px-4 py-2 text-sm ${validation.isValid ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
          {validation.isValid ? "Workflow is valid!" : validation.errors.join("; ")}
          {validation.warnings.length > 0 && <span className="text-amber-600 ml-2">Warnings: {validation.warnings.join("; ")}</span>}
        </div>
      )}

      <div className="flex flex-1 overflow-hidden">
        <div className="w-48 border-r p-2 space-y-1 overflow-y-auto">
          <p className="text-xs font-medium text-muted-foreground mb-2 px-2">Node Types</p>
          {NODE_TYPES.map(nt => (
            <button key={nt.type} className="w-full text-left px-2 py-1.5 text-sm rounded hover:bg-accent transition-colors"
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
          <div className="w-64 border-l p-4 space-y-3">
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
                    <p className="text-sm">{node.nodeType}</p>
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
    </div>
  )
}
