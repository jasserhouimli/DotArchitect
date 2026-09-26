import { useState } from "react"
import { Button } from "@/components/ui/button"
import { workflows, runs, tasks, type TestNodeResult } from "@/api/client"

interface Edge {
  sourceNodeId: string
  targetNodeId: string
}

/** Runs a single node in isolation: optional sample payload + upstream data
 *  from the latest run, with an inline result preview. */
export function NodeTester({ workflowId, nodeId, edges }: {
  workflowId: string
  nodeId: string
  edges: Edge[]
}) {
  const [payload, setPayload] = useState('{\n  "name": "Ada",\n  "age": 36\n}')
  const [useLastRun, setUseLastRun] = useState(true)
  const [testing, setTesting] = useState(false)
  const [result, setResult] = useState<TestNodeResult | null>(null)
  const [error, setError] = useState("")

  const runTest = async () => {
    setTesting(true)
    setError("")
    setResult(null)
    try {
      let samplePayload: unknown = undefined
      if (payload.trim()) {
        try {
          samplePayload = JSON.parse(payload)
        } catch {
          setError("Sample payload is not valid JSON.")
          setTesting(false)
          return
        }
      }
      const inputs: { columns: string[]; rows: (string | null)[][] }[] = []
      if (useLastRun) {
        const runList = await runs.list(workflowId).catch(() => [])
        const latest = runList[0]
        if (latest) {
          const preds = edges.filter(e => e.targetNodeId === nodeId).map(e => e.sourceNodeId)
          const taskList = await runs.tasks(latest.id).catch(() => [])
          for (const p of preds) {
            const t = taskList.find(t => t.nodeId === p)
            if (!t) continue
            const detail = await tasks.get(t.id).catch(() => null)
            if (!detail?.outputJson) continue
            try {
              const data = JSON.parse(detail.outputJson)
              if (Array.isArray(data.columns) && Array.isArray(data.rows)) {
                inputs.push({ columns: data.columns, rows: data.rows.slice(0, 200) })
              }
            } catch { /* predecessor has no tabular output */ }
          }
        }
      }
      setResult(await workflows.testNode(workflowId, nodeId, { samplePayload, inputs }))
    } catch (e) {
      setError(e instanceof Error ? e.message : "Test failed")
    } finally {
      setTesting(false)
    }
  }

  return (
    <div className="border-t pt-3 mt-1 space-y-2">
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium">Test this node</span>
        <Button size="sm" variant="outline" onClick={runTest} disabled={testing}>
          {testing ? "Running…" : "Run test"}
        </Button>
      </div>
      <div>
        <label className="text-xs text-muted-foreground">Sample trigger payload (JSON)</label>
        <textarea
          className="w-full min-h-16 rounded-md border border-input bg-transparent px-2 py-1 text-xs font-mono"
          value={payload}
          onChange={e => setPayload(e.target.value)}
          spellCheck={false}
        />
      </div>
      <label className="flex items-center gap-2 text-xs text-muted-foreground">
        <input type="checkbox" checked={useLastRun} onChange={e => setUseLastRun(e.target.checked)} />
        Feed upstream data from the latest run
      </label>
      {error && <p className="text-xs text-red-600">{error}</p>}
      {result && (
        <div className="text-xs space-y-1">
          <div className={`px-1.5 py-0.5 rounded inline-block ${result.success ? "bg-green-100 text-green-700" : "bg-red-100 text-red-700"}`}>
            {result.success ? `OK · ${result.totalRows} row(s)` : "Failed"}
          </div>
          {result.log && <p className="text-muted-foreground">{result.log}</p>}
          {result.error && <p className="text-red-600">{result.error}</p>}
          {result.columns && result.rows && result.rows.length > 0 && (
            <div className="overflow-auto max-h-48 border rounded">
              <table className="text-[11px]">
                <thead>
                  <tr>{result.columns.map(c => <th key={c} className="px-1.5 py-0.5 text-left bg-accent/50 font-medium whitespace-nowrap">{c}</th>)}</tr>
                </thead>
                <tbody>
                  {result.rows.slice(0, 10).map((r, i) => (
                    <tr key={i} className="border-t">{r.map((v, j) => <td key={j} className="px-1.5 py-0.5 whitespace-nowrap">{v ?? "∅"}</td>)}</tr>
                  ))}
                </tbody>
              </table>
              {result.totalRows > result.rows.length && (
                <p className="px-1.5 py-0.5 text-muted-foreground">…{result.totalRows} rows total</p>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
