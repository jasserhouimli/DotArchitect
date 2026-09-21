import { useState, useEffect } from "react"
import { workspaces, type Workspace } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardHeader, CardTitle } from "@/components/ui/card"

interface DashboardPageProps {
  onSelectWorkspace: (id: string) => void
}

export function DashboardPage({ onSelectWorkspace }: DashboardPageProps) {
  const [list, setList] = useState<Workspace[]>([])
  const [newName, setNewName] = useState("")
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    workspaces.list().then(setList).finally(() => setLoading(false))
  }, [])

  const create = async () => {
    if (!newName.trim()) return
    const id = await workspaces.create({ name: newName })
    setList(prev => [{ id, name: newName, description: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() }, ...prev])
    setNewName("")
  }

  return (
    <div className="max-w-4xl mx-auto p-8">
      <div className="flex items-center justify-between mb-8">
        <h1 className="text-2xl font-bold">Workspaces</h1>
      </div>
      <div className="flex gap-2 mb-6">
        <Input placeholder="New workspace name" value={newName} onChange={e => setNewName(e.target.value)} />
        <Button onClick={create}>Create</Button>
      </div>
      {loading ? <p>Loading...</p> : (
        <div className="grid gap-4">
          {list.map(w => (
            <Card key={w.id} className="cursor-pointer hover:bg-accent/50 transition-colors" onClick={() => onSelectWorkspace(w.id)}>
              <CardHeader>
                <CardTitle className="text-lg">{w.name}</CardTitle>
              </CardHeader>
            </Card>
          ))}
          {list.length === 0 && <p className="text-muted-foreground">No workspaces yet.</p>}
        </div>
      )}
    </div>
  )
}
