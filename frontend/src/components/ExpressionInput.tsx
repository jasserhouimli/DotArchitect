import { useState, useRef } from "react"
import { Input } from "@/components/ui/input"

const SUGGESTIONS = [
  "trigger.kind",
  "trigger.name",
  "trigger.body",
  "run.id",
  "run.version",
]

const HINTS: Record<string, string> = {
  "trigger.kind": "manual, schedule or webhook",
  "trigger.name": "schedule / webhook name",
  "trigger.body": "payload value — keep typing, e.g. order.id",
  "run.id": "current run id",
  "run.version": "published version number",
}

/** Single-line input with {{ }} expression autocomplete. Popup opens while a
 *  token is being typed; Enter picks, Esc closes. */
export function ExpressionInput({ value, onChange, placeholder }: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
}) {
  const [open, setOpen] = useState(false)
  const [opts, setOpts] = useState<string[]>([])
  const [highlight, setHighlight] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)

  const handleChange = (v: string) => {
    onChange(v)
    const pos = inputRef.current?.selectionStart ?? v.length
    const m = v.slice(0, pos).match(/\{\{\s*([a-zA-Z0-9._]*)$/)
    if (!m) {
      setOpen(false)
      return
    }
    const filtered = SUGGESTIONS.filter(s => s.startsWith(m[1]))
    if (filtered.length === 0) {
      setOpen(false)
      return
    }
    setOpts(filtered)
    setHighlight(0)
    setOpen(true)
  }

  const pick = (opt: string) => {
    const el = inputRef.current
    if (!el) return
    const pos = el.selectionStart ?? value.length
    const m = value.slice(0, pos).match(/\{\{\s*([a-zA-Z0-9._]*)$/)
    if (!m) return
    const start = pos - m[0].length
    const insert = `{{ ${opt} }}`
    onChange(value.slice(0, start) + insert + value.slice(pos))
    setOpen(false)
    requestAnimationFrame(() => {
      el.focus()
      el.setSelectionRange(start + insert.length, start + insert.length)
    })
  }

  return (
    <div className="relative">
      <Input
        ref={inputRef}
        value={value}
        placeholder={placeholder}
        onChange={e => handleChange(e.target.value)}
        onBlur={() => setOpen(false)}
        onKeyDown={e => {
          if (!open) return
          if (e.key === "Escape") setOpen(false)
          else if (e.key === "Enter" && opts[highlight]) { e.preventDefault(); pick(opts[highlight]) }
          else if (e.key === "ArrowDown") { e.preventDefault(); setHighlight(h => (h + 1) % opts.length) }
          else if (e.key === "ArrowUp") { e.preventDefault(); setHighlight(h => (h - 1 + opts.length) % opts.length) }
        }}
      />
      {open && (
        <div className="absolute left-0 right-0 top-full mt-1 z-50 rounded-md border bg-white shadow-lg py-1">
          {opts.map((o, i) => (
            <button
              key={o}
              className={`flex w-full items-center justify-between gap-2 px-2 py-1 text-left text-xs font-mono hover:bg-accent ${i === highlight ? "bg-accent" : ""}`}
              onMouseDown={e => {
                e.preventDefault()
                pick(o)
              }}
            >
              <span>{`{{ ${o} }}`}</span>
              <span className="text-muted-foreground font-sans truncate">{HINTS[o]}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
