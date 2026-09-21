import { useState, useEffect, useRef, useCallback } from "react"
import { graph as graphApi, type GraphNode, type GraphEdge } from "@/api/client"
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card"

interface GraphViewProps {
  analysisId: string
}

export function GraphView({ analysisId }: GraphViewProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [nodes, setNodes] = useState<GraphNode[]>([])
  const [edges, setEdges] = useState<GraphEdge[]>([])
  const [selected, setSelected] = useState<GraphNode | null>(null)
  const [cycles, setCycles] = useState<string[][]>([])
  const [positions, setPositions] = useState<Map<string, { x: number; y: number }>>(new Map())

  useEffect(() => {
    graphApi.get(analysisId).then(data => {
      setNodes(data.nodes)
      setEdges(data.edges)
      const pos = new Map<string, { x: number; y: number }>()
      const angle = (2 * Math.PI) / data.nodes.length
      data.nodes.forEach((n, i) => {
        pos.set(n.id, {
          x: 400 + 250 * Math.cos(angle * i - Math.PI / 2),
          y: 300 + 250 * Math.sin(angle * i - Math.PI / 2),
        })
      })
      setPositions(pos)
    })
    graphApi.cycles(analysisId).then(data => setCycles(data.cycles))
  }, [analysisId])

  const draw = useCallback(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    ctx.clearRect(0, 0, canvas.width, canvas.height)

    edges.forEach(edge => {
      const from = positions.get(edge.source)
      const to = positions.get(edge.target)
      if (!from || !to) return
      ctx.beginPath()
      ctx.moveTo(from.x, from.y)
      ctx.lineTo(to.x, to.y)
      ctx.strokeStyle = '#94a3b8'
      ctx.lineWidth = 1.5
      ctx.stroke()

      const angle = Math.atan2(to.y - from.y, to.x - from.x)
      const midX = (from.x + to.x) / 2
      const midY = (from.y + to.y) / 2
      ctx.beginPath()
      ctx.moveTo(midX, midY)
      ctx.lineTo(midX - 8 * Math.cos(angle - 0.4), midY - 8 * Math.sin(angle - 0.4))
      ctx.lineTo(midX - 8 * Math.cos(angle + 0.4), midY - 8 * Math.sin(angle + 0.4))
      ctx.closePath()
      ctx.fillStyle = '#94a3b8'
      ctx.fill()
    })

    nodes.forEach(node => {
      const pos = positions.get(node.id)
      if (!pos) return
      const isSelected = selected?.id === node.id
      const isCycled = cycles.some(c => c.includes(node.name))

      ctx.beginPath()
      ctx.arc(pos.x, pos.y, isSelected ? 24 : 20, 0, 2 * Math.PI)
      ctx.fillStyle = isCycled ? '#fca5a5' : isSelected ? '#93c5fd' : '#e2e8f0'
      ctx.fill()
      ctx.strokeStyle = isSelected ? '#2563eb' : '#64748b'
      ctx.lineWidth = isSelected ? 2 : 1
      ctx.stroke()

      ctx.fillStyle = '#1e293b'
      ctx.font = '11px sans-serif'
      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'
      const label = node.name.length > 15 ? node.name.slice(0, 12) + '...' : node.name
      ctx.fillText(label, pos.x, pos.y)
    })
  }, [nodes, edges, positions, selected, cycles])

  useEffect(() => { draw() }, [draw])

  const handleClick = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current!
    const rect = canvas.getBoundingClientRect()
    const x = e.clientX - rect.left
    const y = e.clientY - rect.top

    for (const node of nodes) {
      const pos = positions.get(node.id)
      if (!pos) continue
      if (Math.hypot(x - pos.x, y - pos.y) < 20) {
        setSelected(node)
        return
      }
    }
    setSelected(null)
  }

  return (
    <div className="flex gap-4">
      <div className="flex-1">
        <canvas ref={canvasRef} width={800} height={600} className="border rounded-lg cursor-crosshair bg-white" onClick={handleClick} />
      </div>
      {selected && (
        <Card className="w-72">
          <CardHeader>
            <CardTitle className="text-base">{selected.name}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <p><strong>Type:</strong> {selected.projectType}</p>
            <p><strong>Frameworks:</strong> {selected.targetFrameworks || 'N/A'}</p>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
