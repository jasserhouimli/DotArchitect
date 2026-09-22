import { useState } from "react"
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"

export type ConfigObject = Record<string, unknown>

interface Props {
  nodeType: string
  config: ConfigObject
  onChange: (config: ConfigObject) => void
}

function TextRow({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <div>
      <label className="text-xs text-muted-foreground">{label}</label>
      <Input value={value} placeholder={placeholder} onChange={e => onChange(e.target.value)} />
    </div>
  )
}

function parseList(text: string): string[] {
  return text.split(",").map(s => s.trim()).filter(s => s.length > 0)
}

export function NodeConfigForm({ nodeType, config, onChange }: Props) {
  const set = (key: string, value: unknown) => {
    const next = { ...config }
    if (value === undefined || value === "" || value === null) delete next[key]
    else next[key] = value
    onChange(next)
  }

  switch (nodeType) {
    case "data.csv.read": {
      const [dedupeText, setDedupeText] = useState(((config.dedupeColumns as string[] | undefined) || []).join(", "))
      return (
        <div className="space-y-3">
          <div>
            <label className="text-xs text-muted-foreground">CSV content (required)</label>
            <textarea
              className="w-full h-32 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
              value={(config.csvText as string) || ""}
              placeholder={"name,age\nAda,36\nGrace,85"}
              onChange={e => set("csvText", e.target.value)}
            />
          </div>
          <div className="flex gap-2">
            <div className="flex-1">
              <label className="text-xs text-muted-foreground">Delimiter</label>
              <Input value={(config.delimiter as string) || ","} maxLength={1} onChange={e => set("delimiter", e.target.value || ",")} />
            </div>
            <div className="flex items-end gap-1 pb-2">
              <input type="checkbox" id="hasHeader" checked={(config.hasHeader as boolean | undefined) ?? true}
                onChange={e => set("hasHeader", e.target.checked)} />
              <label htmlFor="hasHeader" className="text-xs">Header row</label>
            </div>
          </div>
          <TextRow label="Deduplicate on columns (comma-separated, optional)" value={dedupeText}
            onChange={v => { setDedupeText(v); set("dedupeColumns", parseList(v)) }} />
        </div>
      )
    }

    case "http.request":
      return (
        <div className="space-y-3">
          <TextRow label="URL (required, http/https GET)" value={(config.url as string) || ""}
            placeholder="https://api.example.com/items" onChange={v => set("url", v)} />
          <TextRow label="Timeout seconds (1-120)" value={String((config.timeoutSeconds as number) ?? 30)}
            onChange={v => { const n = parseInt(v, 10); set("timeoutSeconds", isNaN(n) ? 30 : Math.min(120, Math.max(1, n))) }} />
          <p className="text-xs text-muted-foreground">Private/loopback hosts are blocked. Response must be JSON, max 2 MB.</p>
        </div>
      )

    case "data.validate": {
      const [cols, setCols] = useState(((config.requiredColumns as string[] | undefined) || []).join(", "))
      return (
        <div className="space-y-3">
          <TextRow label="Required columns (comma-separated)" value={cols}
            placeholder="orderId,total"
            onChange={v => { setCols(v); set("requiredColumns", parseList(v)) }} />
          <p className="text-xs text-muted-foreground">Rows with empty required fields are rejected; the rest pass through.</p>
        </div>
      )
    }

    case "data.filter": {
      const ops = ["equals", "notEquals", "contains", "notContains", "greaterThan", "lessThan", "isEmpty", "isNotEmpty"]
      const op = (config.operator as string) || "equals"
      const needsValue = op !== "isEmpty" && op !== "isNotEmpty"
      return (
        <div className="space-y-3">
          <TextRow label="Column (required)" value={(config.column as string) || ""} onChange={v => set("column", v)} />
          <div>
            <label className="text-xs text-muted-foreground">Operator</label>
            <select className="w-full h-9 rounded-md border border-input bg-transparent px-3 text-sm"
              value={op} onChange={e => set("operator", e.target.value)}>
              {ops.map(o => <option key={o} value={o}>{o}</option>)}
            </select>
          </div>
          {needsValue && (
            <TextRow label="Value" value={config.value === undefined || config.value === null ? "" : String(config.value)}
              onChange={v => set("value", v)} />
          )}
        </div>
      )
    }

    case "data.transform": {
      const [selectText, setSelectText] = useState(((config.select as string[] | undefined) || []).join(", "))
      const [upperText, setUpperText] = useState(((config.upperColumns as string[] | undefined) || []).join(", "))
      const [lowerText, setLowerText] = useState(((config.lowerColumns as string[] | undefined) || []).join(", "))
      const [renamesText, setRenamesText] = useState(
        Object.entries((config.renames as Record<string, string> | undefined) || {}).map(([k, v]) => `${k}=${v}`).join("\n"))
      const parseRenames = (text: string) => {
        const obj: Record<string, string> = {}
        text.split("\n").forEach(line => {
          const i = line.indexOf("=")
          if (i > 0) obj[line.slice(0, i).trim()] = line.slice(i + 1).trim()
        })
        return obj
      }
      return (
        <div className="space-y-3">
          <TextRow label="Select columns (comma-separated, empty = keep all)" value={selectText}
            onChange={v => { setSelectText(v); set("select", parseList(v)) }} />
          <div>
            <label className="text-xs text-muted-foreground">Rename (one old=new per line)</label>
            <textarea className="w-full h-16 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
              value={renamesText}
              onChange={e => { setRenamesText(e.target.value); set("renames", parseRenames(e.target.value)) }} />
          </div>
          <TextRow label="Uppercase columns" value={upperText}
            onChange={v => { setUpperText(v); set("upperColumns", parseList(v)) }} />
          <TextRow label="Lowercase columns" value={lowerText}
            onChange={v => { setLowerText(v); set("lowerColumns", parseList(v)) }} />
        </div>
      )
    }

    case "data.aggregate": {
      const ops = (config.operations as Array<{ column?: string; operation: string; alias?: string }>) || []
      const aggOps = ["count", "sum", "avg", "min", "max"]
      const [groupText, setGroupText] = useState(((config.groupBy as string[] | undefined) || []).join(", "))
      const updateOp = (i: number, patch: Partial<{ column: string; operation: string; alias: string }>) => {
        const next = ops.map((o, j) => j === i ? { ...o, ...patch } : o)
        set("operations", next)
      }
      return (
        <div className="space-y-3">
          <TextRow label="Group by columns (comma-separated, empty = single group)" value={groupText}
            onChange={v => { setGroupText(v); set("groupBy", parseList(v)) }} />
          <div className="space-y-2">
            <label className="text-xs text-muted-foreground">Operations (required)</label>
            {ops.map((o, i) => (
              <div key={i} className="flex gap-1">
                <Input className="flex-1" placeholder="column" value={o.column || ""} onChange={e => updateOp(i, { column: e.target.value })} />
                <select className="h-9 rounded-md border border-input bg-transparent px-1 text-xs"
                  value={o.operation} onChange={e => updateOp(i, { operation: e.target.value })}>
                  {aggOps.map(a => <option key={a} value={a}>{a}</option>)}
                </select>
                <Input className="w-20" placeholder="alias" value={o.alias || ""} onChange={e => updateOp(i, { alias: e.target.value })} />
                <Button size="sm" variant="ghost" onClick={() => set("operations", ops.filter((_, j) => j !== i))}>x</Button>
              </div>
            ))}
            <Button size="sm" variant="outline" onClick={() => set("operations", [...ops, { operation: "count" }])}>Add operation</Button>
          </div>
        </div>
      )
    }

    case "data.output":
      return (
        <div className="space-y-3">
          <div>
            <label className="text-xs text-muted-foreground">Format</label>
            <select className="w-full h-9 rounded-md border border-input bg-transparent px-3 text-sm"
              value={(config.format as string) || "json"} onChange={e => set("format", e.target.value)}>
              <option value="json">json</option>
              <option value="csv">csv</option>
            </select>
          </div>
          <p className="text-xs text-muted-foreground">Saves the incoming dataset as a downloadable artifact.</p>
        </div>
      )

    default:
      return <p className="text-xs text-muted-foreground">No configurable options for this node type.</p>
  }
}

export function defaultConfig(nodeType: string): ConfigObject {
  switch (nodeType) {
    case "data.csv.read": return { csvText: "", delimiter: ",", hasHeader: true }
    case "http.request": return { url: "", timeoutSeconds: 30 }
    case "data.validate": return { requiredColumns: [] }
    case "data.filter": return { column: "", operator: "equals", value: "" }
    case "data.transform": return {}
    case "data.aggregate": return { groupBy: [], operations: [] }
    case "data.output": return { format: "json" }
    default: return {}
  }
}

export function parseConfig(json: string | null): ConfigObject {
  if (!json) return {}
  try {
    const parsed = JSON.parse(json)
    return typeof parsed === "object" && parsed !== null ? parsed as ConfigObject : {}
  } catch {
    return {}
  }
}
