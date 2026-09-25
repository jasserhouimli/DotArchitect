import { useState, useEffect } from "react"
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { workflows, type WorkflowFile } from "@/api/client"

export type ConfigObject = Record<string, unknown>

interface Props {
  nodeType: string
  config: ConfigObject
  workflowId: string
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

function CheckRow({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex items-center gap-2 text-xs">
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />
      {label}
    </label>
  )
}

function parseList(text: string): string[] {
  return text.split(",").map(s => s.trim()).filter(s => s.length > 0)
}

function SourceToggle({ source, onChange }: { source: string; onChange: (v: string) => void }) {
  return (
    <div>
      <label className="text-xs text-muted-foreground">Source</label>
      <select className="w-full h-9 rounded-md border border-input bg-transparent px-3 text-sm"
        value={source} onChange={e => onChange(e.target.value)}>
        <option value="text">Pasted content</option>
        <option value="upload">Uploaded file</option>
      </select>
    </div>
  )
}

function FilePicker({ workflowId, accept, fileId, onUploaded, onClear }: {
  workflowId: string; accept: string; fileId: string;
  onUploaded: (f: WorkflowFile) => void; onClear: () => void;
}) {
  const [uploading, setUploading] = useState(false)
  const [uploadError, setUploadError] = useState("")
  const [info, setInfo] = useState<WorkflowFile | null>(null)

  useEffect(() => {
    if (!fileId) { setInfo(null); return }
    workflows.listFiles(workflowId).then(fs => setInfo(fs.find(f => f.fileId === fileId) || null)).catch(() => {})
  }, [workflowId, fileId])

  const pick = async (file: File | undefined) => {
    if (!file) return
    setUploading(true)
    setUploadError("")
    try {
      const saved = await workflows.uploadFile(workflowId, file)
      setInfo(saved)
      onUploaded(saved)
    } catch (e) {
      setUploadError(e instanceof Error ? e.message : "Upload failed")
    } finally {
      setUploading(false)
    }
  }

  return (
    <div className="space-y-2 rounded-md border p-2">
      {info
        ? <div className="text-xs">
            <div className="font-medium truncate">{info.fileName}</div>
            <div className="text-muted-foreground">{info.rows} rows · {info.columns.length} cols · {(info.size / 1024).toFixed(1)} KB</div>
            <Button size="sm" variant="ghost" className="h-6 px-1 text-xs" onClick={() => { setInfo(null); onClear() }}>Use pasted content instead</Button>
          </div>
        : <>
            <label className="text-xs text-muted-foreground">Upload {accept} (max 10 MB)</label>
            <Input type="file" accept={accept} disabled={uploading}
              onChange={e => pick(e.target.files?.[0])} />
            {uploading && <p className="text-xs text-muted-foreground">Uploading…</p>}
          </>}
      {uploadError && <p className="text-xs text-red-600">{uploadError}</p>}
    </div>
  )
}

export function NodeConfigForm({ nodeType, config, workflowId, onChange }: Props) {
  const set = (key: string, value: unknown) => {
    const next = { ...config }
    if (value === undefined || value === "" || value === null) delete next[key]
    else next[key] = value
    onChange(next)
  }

  // Batched version: sequential set() calls each read stale state and drop
  // each other's changes, so multi-field updates must go through one onChange.
  const setMany = (patch: Record<string, unknown>) => {
    const next = { ...config }
    for (const [k, v] of Object.entries(patch)) {
      if (v === undefined || v === "" || v === null) delete next[k]
      else next[k] = v
    }
    onChange(next)
  }

  switch (nodeType) {
    case "data.csv.read": {
      const source = (config.source as string) || "text"
      const [dedupeText, setDedupeText] = useState(((config.dedupeColumns as string[] | undefined) || []).join(", "))
      const [nullsText, setNullsText] = useState(((config.nullValues as string[] | undefined) || []).join(", "))
      return (
        <div className="space-y-3">
          <SourceToggle source={source} onChange={v => {
            if (v === "upload") setMany({ source: v, csvText: "" })
            else setMany({ source: v, fileId: "", fileName: "" })
          }} />
          {source === "upload" ? (
            <FilePicker workflowId={workflowId} accept=".csv,.txt" fileId={(config.fileId as string) || ""}
              onUploaded={f => setMany({ fileId: f.fileId, fileName: f.fileName })}
              onClear={() => { set("source", "text") }} />
          ) : (
            <div>
              <label className="text-xs text-muted-foreground">CSV content (required)</label>
              <textarea
                className="w-full h-32 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
                value={(config.csvText as string) || ""}
                placeholder={"name,age\nAda,36\nGrace,85"}
                onChange={e => set("csvText", e.target.value)}
              />
            </div>
          )}
          <div className="flex gap-2">
            <div className="flex-1">
              <label className="text-xs text-muted-foreground">Delimiter</label>
              <Input value={(config.delimiter as string) || ","} maxLength={1} onChange={e => set("delimiter", e.target.value || ",")} />
            </div>
            <TextRow label="Skip rows" value={String((config.skipRows as number) ?? 0)}
              onChange={v => set("skipRows", Math.max(0, parseInt(v, 10) || 0))} />
            <TextRow label="Max rows" value={String((config.maxRows as number) ?? "")} placeholder="50000"
              onChange={v => set("maxRows", v === "" ? undefined : Math.min(50000, Math.max(1, parseInt(v, 10) || 1)))} />
          </div>
          <div className="flex gap-4">
            <CheckRow label="Header row" checked={(config.hasHeader as boolean | undefined) ?? true} onChange={v => set("hasHeader", v)} />
            <CheckRow label="Trim cells" checked={(config.trim as boolean | undefined) ?? true} onChange={v => set("trim", v)} />
          </div>
          <TextRow label="Null values (comma-separated, empty = none extra)" value={nullsText}
            placeholder="NA, null, -"
            onChange={v => { setNullsText(v); set("nullValues", parseList(v)) }} />
          <TextRow label="Deduplicate on columns (optional)" value={dedupeText}
            onChange={v => { setDedupeText(v); set("dedupeColumns", parseList(v)) }} />
        </div>
      )
    }

    case "data.json.read": {
      const source = (config.source as string) || "text"
      return (
        <div className="space-y-3">
          <div>
            <label className="text-xs text-muted-foreground">Source</label>
            <select className="w-full h-9 rounded-md border border-input bg-transparent px-3 text-sm"
              value={source} onChange={e => {
                const v = e.target.value
                if (v === "upload") setMany({ source: v, jsonText: "" })
                else if (v === "input") setMany({ source: v, jsonText: "", fileId: "", fileName: "" })
                else setMany({ source: v, fileId: "", fileName: "" })
              }}>
              <option value="text">Pasted content</option>
              <option value="upload">Uploaded file</option>
              <option value="input">Upstream input (parse a column)</option>
            </select>
          </div>
          {source === "input" ? (
            <TextRow label="Column holding JSON (required)" value={(config.column as string) || ""}
              placeholder="payload"
              onChange={v => set("column", v)} />
          ) : source === "upload" ? (
            <FilePicker workflowId={workflowId} accept=".json" fileId={(config.fileId as string) || ""}
              onUploaded={f => setMany({ fileId: f.fileId, fileName: f.fileName })}
              onClear={() => { set("source", "text") }} />
          ) : (
            <div>
              <label className="text-xs text-muted-foreground">JSON content (required)</label>
              <textarea
                className="w-full h-32 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
                value={(config.jsonText as string) || ""}
                placeholder={'[{"id": 1, "total": 42}]'}
                onChange={e => set("jsonText", e.target.value)}
              />
            </div>
          )}
          <TextRow label="Root path (optional, e.g. data.orders)" value={(config.rootPath as string) || ""}
            placeholder="data.orders.0"
            onChange={v => set("rootPath", v)} />
          {source === "input" && (
            <p className="text-xs text-muted-foreground">
              Objects merge into each row (extracted fields win on name clashes); arrays explode into one row per item; bad cells are rejected with counts.
            </p>
          )}
        </div>
      )
    }

    case "http.request": {
      const [headersText, setHeadersText] = useState(
        Object.entries((config.headers as Record<string, string> | undefined) || {}).map(([k, v]) => `${k}: ${v}`).join("\n"))
      const parseHeaders = (text: string) => {
        const obj: Record<string, string> = {}
        text.split("\n").forEach(line => {
          const i = line.indexOf(":")
          if (i > 0) obj[line.slice(0, i).trim()] = line.slice(i + 1).trim()
        })
        return obj
      }
      const paging = (config.pagination as Record<string, unknown> | undefined) || null
      return (
        <div className="space-y-3">
          <TextRow label="URL (required, http/https GET)" value={(config.url as string) || ""}
            placeholder="https://api.example.com/items" onChange={v => set("url", v)} />
          <TextRow label="Timeout seconds (1-120)" value={String((config.timeoutSeconds as number) ?? 30)}
            onChange={v => { const n = parseInt(v, 10); set("timeoutSeconds", isNaN(n) ? 30 : Math.min(120, Math.max(1, n))) }} />
          <div>
            <label className="text-xs text-muted-foreground">Headers (one Name: value per line; auth headers blocked)</label>
            <textarea className="w-full h-14 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
              value={headersText} placeholder="Accept-Language: en"
              onChange={e => { setHeadersText(e.target.value); set("headers", parseHeaders(e.target.value)) }} />
          </div>
          <TextRow label="Root path (optional)" value={(config.rootPath as string) || ""}
            placeholder="data.orders" onChange={v => set("rootPath", v)} />
          <div className="rounded-md border p-2 space-y-2">
            <CheckRow label="Offset pagination" checked={paging !== null}
              onChange={v => set("pagination", v ? { mode: "offset", param: "page", start: 1, step: 1, maxPages: 5 } : undefined)} />
            {paging !== null && (
              <div className="grid grid-cols-2 gap-2">
                <TextRow label="Query param" value={String(paging.param ?? "page")}
                  onChange={v => set("pagination", { ...paging, param: v || "page" })} />
                <TextRow label="Max pages (1-20)" value={String(paging.maxPages ?? 5)}
                  onChange={v => set("pagination", { ...paging, maxPages: Math.min(20, Math.max(1, parseInt(v, 10) || 1)) })} />
                <TextRow label="Start" value={String(paging.start ?? 1)}
                  onChange={v => set("pagination", { ...paging, start: parseInt(v, 10) || 0 })} />
                <TextRow label="Step" value={String(paging.step ?? 1)}
                  onChange={v => set("pagination", { ...paging, step: Math.max(1, parseInt(v, 10) || 1) })} />
              </div>
            )}
          </div>
          <p className="text-xs text-muted-foreground">Private/loopback hosts are blocked. Responses must be JSON, max 2 MB per page.</p>
        </div>
      )
    }

    case "data.validate": {
      const [cols, setCols] = useState(((config.requiredColumns as string[] | undefined) || []).join(", "))
      const [uniqText, setUniqText] = useState(((config.uniqueColumns as string[] | undefined) || []).join(", "))
      const [typesText, setTypesText] = useState(
        Object.entries((config.columnTypes as Record<string, string> | undefined) || {}).map(([k, v]) => `${k}: ${v}`).join("\n"))
      const parseTypes = (text: string) => {
        const obj: Record<string, string> = {}
        text.split("\n").forEach(line => {
          const i = line.indexOf(":")
          if (i > 0) obj[line.slice(0, i).trim()] = line.slice(i + 1).trim().toLowerCase()
        })
        return obj
      }
      return (
        <div className="space-y-3">
          <TextRow label="Required columns (comma-separated)" value={cols}
            placeholder="orderId,total"
            onChange={v => { setCols(v); set("requiredColumns", parseList(v)) }} />
          <div>
            <label className="text-xs text-muted-foreground">Column types (one name: type per line — string, number, integer, boolean, date)</label>
            <textarea className="w-full h-16 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
              value={typesText} placeholder={"amount: number\ncreated: date"}
              onChange={e => { setTypesText(e.target.value); set("columnTypes", parseTypes(e.target.value)) }} />
          </div>
          <TextRow label="Unique columns (duplicates rejected)" value={uniqText}
            onChange={v => { setUniqText(v); set("uniqueColumns", parseList(v)) }} />
        </div>
      )
    }

    case "data.filter": {
      const ops = ["equals", "notEquals", "contains", "notContains", "startsWith", "endsWith", "matches", "inList", "greaterThan", "lessThan", "isEmpty", "isNotEmpty"]
      const op = (config.operator as string) || "equals"
      const needsValue = op !== "isEmpty" && op !== "isNotEmpty"
      const [listText, setListText] = useState(
        Array.isArray(config.value) ? (config.value as unknown[]).map(String).join(", ") : ""
      )
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
          {op === "inList" ? (
            <TextRow label="Values (comma-separated)" value={listText}
              onChange={v => { setListText(v); set("value", parseList(v)) }} />
          ) : needsValue ? (
            <TextRow label={op === "matches" ? "Regex pattern" : "Value"} value={config.value === undefined || config.value === null ? "" : String(config.value)}
              placeholder={op === "matches" ? "^A.*$" : ""}
              onChange={v => set("value", v)} />
          ) : null}
        </div>
      )
    }

    case "data.transform": {
      const [selectText, setSelectText] = useState(((config.select as string[] | undefined) || []).join(", "))
      const [dropText, setDropText] = useState(((config.dropColumns as string[] | undefined) || []).join(", "))
      const [upperText, setUpperText] = useState(((config.upperColumns as string[] | undefined) || []).join(", "))
      const [lowerText, setLowerText] = useState(((config.lowerColumns as string[] | undefined) || []).join(", "))
      const [renamesText, setRenamesText] = useState(
        Object.entries((config.renames as Record<string, string> | undefined) || {}).map(([k, v]) => `${k}=${v}`).join("\n"))
      const [fillText, setFillText] = useState(
        Object.entries((config.fillNull as Record<string, unknown> | undefined) || {}).map(([k, v]) => `${k}=${String(v)}`).join("\n"))
      const [roundText, setRoundText] = useState(
        Object.entries((config.round as Record<string, number> | undefined) || {}).map(([k, v]) => `${k}=${v}`).join("\n"))
      const parsePairs = (text: string) => {
        const obj: Record<string, string> = {}
        text.split("\n").forEach(line => {
          const i = line.indexOf("=")
          if (i > 0) obj[line.slice(0, i).trim()] = line.slice(i + 1).trim()
        })
        return obj
      }
      const concat = (config.concat as { sources?: string[]; separator?: string; alias?: string } | undefined) || null
      return (
        <div className="space-y-3">
          <TextRow label="Select columns (empty = keep all)" value={selectText}
            onChange={v => { setSelectText(v); set("select", parseList(v)) }} />
          <TextRow label="Drop columns" value={dropText}
            onChange={v => { setDropText(v); set("dropColumns", parseList(v)) }} />
          <div>
            <label className="text-xs text-muted-foreground">Rename (one old=new per line)</label>
            <textarea className="w-full h-16 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
              value={renamesText}
              onChange={e => { setRenamesText(e.target.value); set("renames", parsePairs(e.target.value)) }} />
          </div>
          <TextRow label="Uppercase columns" value={upperText}
            onChange={v => { setUpperText(v); set("upperColumns", parseList(v)) }} />
          <TextRow label="Lowercase columns" value={lowerText}
            onChange={v => { setLowerText(v); set("lowerColumns", parseList(v)) }} />
          <div>
            <label className="text-xs text-muted-foreground">Fill nulls (one column=value per line)</label>
            <textarea className="w-full h-12 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
              value={fillText}
              onChange={e => { setFillText(e.target.value); set("fillNull", parsePairs(e.target.value)) }} />
          </div>
          <div>
            <label className="text-xs text-muted-foreground">Round numbers (one column=decimals per line)</label>
            <textarea className="w-full h-12 rounded-md border border-input bg-transparent px-3 py-2 text-xs font-mono"
              value={roundText}
              onChange={e => {
                setRoundText(e.target.value)
                const obj: Record<string, number> = {}
                e.target.value.split("\n").forEach(line => {
                  const i = line.indexOf("=")
                  if (i > 0) obj[line.slice(0, i).trim()] = Math.min(10, Math.max(0, parseInt(line.slice(i + 1).trim(), 10) || 0))
                })
                set("round", obj)
              }} />
          </div>
          <div className="rounded-md border p-2 space-y-2">
            <CheckRow label="Concatenate columns" checked={concat !== null}
              onChange={v => set("concat", v ? { sources: [], separator: " ", alias: "" } : undefined)} />
            {concat !== null && (
              <>
                <TextRow label="Source columns (comma-separated)" value={(concat.sources || []).join(", ")}
                  onChange={v => set("concat", { ...concat, sources: parseList(v) })} />
                <div className="flex gap-2">
                  <div className="flex-1">
                    <label className="text-xs text-muted-foreground">Separator</label>
                    <Input value={concat.separator ?? " "} onChange={e => set("concat", { ...concat, separator: e.target.value })} />
                  </div>
                  <div className="flex-1">
                    <label className="text-xs text-muted-foreground">New column name</label>
                    <Input value={concat.alias || ""} onChange={e => set("concat", { ...concat, alias: e.target.value })} />
                  </div>
                </div>
              </>
            )}
          </div>
          <p className="text-xs text-muted-foreground">All column names refer to the input columns. Order: select/drop → rename → case → fill → round → concat.</p>
        </div>
      )
    }

    case "data.aggregate": {
      const ops = (config.operations as Array<{ column?: string; operation: string; alias?: string }>) || []
      const aggOps = ["count", "countDistinct", "sum", "avg", "min", "max", "median"]
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

    case "data.sort": {
      const keys = (config.orderBy as Array<{ column?: string; direction?: string }>) || []
      const updateKey = (i: number, patch: Partial<{ column: string; direction: string }>) => {
        set("orderBy", keys.map((k, j) => j === i ? { ...k, ...patch } : k))
      }
      return (
        <div className="space-y-3">
          <label className="text-xs text-muted-foreground">Sort keys (required, applied in order)</label>
          {keys.map((k, i) => (
            <div key={i} className="flex gap-1">
              <Input className="flex-1" placeholder="column" value={k.column || ""} onChange={e => updateKey(i, { column: e.target.value })} />
              <select className="h-9 rounded-md border border-input bg-transparent px-2 text-xs"
                value={k.direction || "asc"} onChange={e => updateKey(i, { direction: e.target.value })}>
                <option value="asc">asc</option>
                <option value="desc">desc</option>
              </select>
              <Button size="sm" variant="ghost" onClick={() => set("orderBy", keys.filter((_, j) => j !== i))}>x</Button>
            </div>
          ))}
          <Button size="sm" variant="outline" onClick={() => set("orderBy", [...keys, { column: "", direction: "asc" }])}>Add key</Button>
        </div>
      )
    }

    case "data.limit": {
      const count = (config.count as number | undefined) ?? 100
      const offset = (config.offset as number | undefined) ?? 0
      return (
        <div className="space-y-3">
          <TextRow label="Count (required, 0 or more)" value={String(count)}
            onChange={v => set("count", Math.max(0, parseInt(v, 10) || 0))} />
          <TextRow label="Offset" value={String(offset)}
            onChange={v => set("offset", Math.max(0, parseInt(v, 10) || 0))} />
        </div>
      )
    }

    case "data.dedupe": {
      const [colsText, setColsText] = useState(((config.columns as string[] | undefined) || []).join(", "))
      return (
        <div className="space-y-3">
          <TextRow label="Key columns (comma-separated, empty = whole row)" value={colsText}
            onChange={v => { setColsText(v); set("columns", parseList(v)) }} />
          <p className="text-xs text-muted-foreground">Keeps the first of each duplicate group.</p>
        </div>
      )
    }

    case "data.join": {
      const how = (config.how as string) || "inner"
      const useOn = config.on !== undefined || (config.leftOn === undefined && config.rightOn === undefined)
      const [onText, setOnText] = useState(((config.on as string[] | undefined) || []).join(", "))
      const [leftText, setLeftText] = useState(((config.leftOn as string[] | undefined) || []).join(", "))
      const [rightText, setRightText] = useState(((config.rightOn as string[] | undefined) || []).join(", "))
      return (
        <div className="space-y-3">
          <div>
            <label className="text-xs text-muted-foreground">Join type</label>
            <select className="w-full h-9 rounded-md border border-input bg-transparent px-3 text-sm"
              value={how} onChange={e => set("how", e.target.value)}>
              <option value="inner">inner (only matches)</option>
              <option value="left">left (keep all left rows)</option>
            </select>
          </div>
          <CheckRow label="Same key names on both sides" checked={useOn}
            onChange={v => {
              if (v) setMany({ on: parseList(onText), leftOn: undefined, rightOn: undefined })
              else setMany({ on: undefined, leftOn: parseList(leftText), rightOn: parseList(rightText) })
            }} />
          {useOn ? (
            <TextRow label="Key columns (required)" value={onText}
              onChange={v => { setOnText(v); set("on", parseList(v)) }} />
          ) : (
            <>
              <TextRow label="Left keys" value={leftText}
                onChange={v => { setLeftText(v); set("leftOn", parseList(v)) }} />
              <TextRow label="Right keys" value={rightText}
                onChange={v => { setRightText(v); set("rightOn", parseList(v)) }} />
            </>
          )}
          <p className="text-xs text-muted-foreground">Requires exactly two inputs: left = first connection, right = second.</p>
        </div>
      )
    }

    case "data.profile": {
      const [colsText, setColsText] = useState(((config.columns as string[] | undefined) || []).join(", "))
      return (
        <div className="space-y-3">
          <TextRow label="Columns (comma-separated, empty = all)" value={colsText}
            onChange={v => { setColsText(v); set("columns", parseList(v)) }} />
          <p className="text-xs text-muted-foreground">Outputs one row per column: count, nulls, distinct, min, max, mean.</p>
        </div>
      )
    }

    case "trigger.payload":
      return (
        <div className="space-y-3">
          <TextRow label="Root path (optional, e.g. order.id)" value={(config.rootPath as string) || ""}
            placeholder="items.0"
            onChange={v => set("rootPath", v)} />
          <p className="text-xs text-muted-foreground">Parses the JSON payload delivered by the schedule/webhook trigger that started this run. Fails on manual runs without a payload.</p>
        </div>
      )

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
          <TextRow label="File name (optional, defaults to node name)" value={(config.fileName as string) || ""}
            placeholder="report"
            onChange={v => set("fileName", v)} />
          {(config.format as string) === "csv" && (
            <div className="flex gap-2">
              <div className="flex-1">
                <label className="text-xs text-muted-foreground">Delimiter</label>
                <Input value={(config.delimiter as string) || ","} maxLength={1} onChange={e => set("delimiter", e.target.value || ",")} />
              </div>
              <div className="flex items-end pb-2">
                <CheckRow label="Header row" checked={(config.includeHeader as boolean | undefined) ?? true}
                  onChange={v => set("includeHeader", v)} />
              </div>
            </div>
          )}
          <p className="text-xs text-muted-foreground">Saves the incoming dataset as a downloadable artifact.</p>
        </div>
      )

    default:
      return <p className="text-xs text-muted-foreground">No configurable options for this node type.</p>
  }
}

export function defaultConfig(nodeType: string): ConfigObject {
  switch (nodeType) {
    case "data.csv.read": return { source: "text", csvText: "", delimiter: ",", hasHeader: true, trim: true }
    case "data.json.read": return { source: "text", jsonText: "", rootPath: "" }
    case "http.request": return { url: "", timeoutSeconds: 30 }
    case "data.validate": return { requiredColumns: [] }
    case "data.filter": return { column: "", operator: "equals", value: "" }
    case "data.transform": return {}
    case "data.aggregate": return { groupBy: [], operations: [] }
    case "data.sort": return { orderBy: [] }
    case "data.limit": return { count: 100, offset: 0 }
    case "data.dedupe": return { columns: [] }
    case "data.join": return { how: "inner", on: [] }
    case "data.profile": return { columns: [] }
    case "trigger.payload": return { rootPath: "" }
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
