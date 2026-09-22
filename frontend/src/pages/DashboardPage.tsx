import { useState, useEffect } from "react"
import { workflows, type Workflow } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardHeader, CardTitle } from "@/components/ui/card"

interface DashboardPageProps {
  user: { id: string; email: string; displayName: string }
  onSelectWorkflow: (id: string) => void
  onLogout: () => void
}

export function DashboardPage({ user, onSelectWorkflow, onLogout }: DashboardPageProps) {
  const [list, setList] = useState<Workflow[]>([])
  const [newName, setNewName] = useState("")
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    workflows.list().then(setList).finally(() => setLoading(false))
  }, [])

  const create = async () => {
    if (!newName.trim()) return
    const id = await workflows.create({ name: newName })
    setList(prev => [{ id, name: newName, description: null, status: "Draft", currentVersion: 0, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() }, ...prev])
    setNewName("")
  }

  const remove = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation()
    await workflows.delete(id)
    setList(prev => prev.filter(w => w.id !== id))
  }

  return (
    <div className="max-w-4xl mx-auto p-8">
      <div className="flex items-center justify-between mb-8">
        <h1 className="text-2xl font-bold">Reflow</h1>
        <div className="flex items-center gap-4">
          <span className="text-sm text-muted-foreground">{user.email}</span>
          <Button variant="ghost" size="sm" onClick={onLogout}>Logout</Button>
        </div>
      </div>
      <div className="flex gap-2 mb-6">
        <Input placeholder="New workflow name" value={newName} onChange={e => setNewName(e.target.value)} onKeyDown={e => e.key === 'Enter' && create()} />
        <Button onClick={create}>Create</Button>
      </div>
      {loading ? <p>Loading...</p> : (
        <div className="grid gap-4">
          {list.map(w => (
            <Card key={w.id} className="cursor-pointer hover:bg-accent/50 transition-colors" onClick={() => onSelectWorkflow(w.id)}>
              <CardHeader className="py-3">
                <div className="flex items-center justify-between">
                  <CardTitle className="text-lg">{w.name}</CardTitle>
                  <div className="flex items-center gap-2">
                    <span className={`text-xs px-2 py-0.5 rounded ${w.status === 'Published' ? 'bg-green-100 text-green-700' : w.status === 'Archived' ? 'bg-gray-100 text-gray-700' : 'bg-blue-100 text-blue-700'}`}>
                      {w.status}
                    </span>
                    <Button variant="ghost" size="sm" className="text-destructive h-7" onClick={(e) => remove(w.id, e)}>x</Button>
                  </div>
                </div>
                {w.description && <p className="text-sm text-muted-foreground mt-1">{w.description}</p>}
              </CardHeader>
            </Card>
          ))}
          {list.length === 0 && <p className="text-muted-foreground">No workflows yet. Create one to start.</p>}
        </div>
      )}
    </div>
  )
}
