import { useState, useEffect } from "react"
import { analyses, type Analysis } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card"
import { GraphView } from "./GraphView"

interface WorkspacePageProps {
  workspaceId: string
  onBack: () => void
}

export function WorkspacePage({ workspaceId, onBack }: WorkspacePageProps) {
  const [analysesList, setAnalysesList] = useState<Analysis[]>([])
  const [selectedAnalysis, setSelectedAnalysis] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [uploading, setUploading] = useState(false)

  useEffect(() => {
    analyses.list(workspaceId).then(setAnalysesList).finally(() => setLoading(false))
  }, [workspaceId])

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true)
    try {
      const result = await analyses.upload(workspaceId, file)
      const analysis = await analyses.get(result.id)
      setAnalysesList(prev => [analysis, ...prev])
      setSelectedAnalysis(result.id)
    } finally {
      setUploading(false)
    }
  }

  return (
    <div className="max-w-6xl mx-auto p-8">
      <div className="flex items-center gap-4 mb-8">
        <Button variant="ghost" onClick={onBack}>Back</Button>
        <h1 className="text-2xl font-bold">Workspace</h1>
      </div>

      <div className="mb-6">
        <label className="block">
          <Button variant="outline" disabled={uploading}>
            {uploading ? "Uploading..." : "Upload .NET Solution ZIP"}
          </Button>
          <input type="file" accept=".zip" className="hidden" onChange={handleUpload} />
        </label>
      </div>

      {loading ? <p>Loading...</p> : (
        <div className="grid gap-4 mb-8">
          {analysesList.map(a => (
            <Card
              key={a.id}
              className={`cursor-pointer transition-colors ${selectedAnalysis === a.id ? 'ring-2 ring-primary' : 'hover:bg-accent/50'}`}
              onClick={() => setSelectedAnalysis(a.id)}
            >
              <CardHeader>
                <CardTitle className="text-base">{a.originalFileName}</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex gap-4 text-sm text-muted-foreground">
                  <span>Status: {a.status === 2 ? 'Completed' : a.status === 1 ? 'Processing...' : a.status === 3 ? 'Failed' : 'Pending'}</span>
                  <span>Projects: {a.projectCount}</span>
                  <span>References: {a.referenceCount}</span>
                  <span>Warnings: {a.warningCount}</span>
                </div>
              </CardContent>
            </Card>
          ))}
          {analysesList.length === 0 && <p className="text-muted-foreground">No analyses yet. Upload a ZIP to get started.</p>}
        </div>
      )}

      {selectedAnalysis && (
        <div className="space-y-6">
          <h2 className="text-lg font-semibold">Dependency Graph</h2>
          <GraphView analysisId={selectedAnalysis} />
        </div>
      )}
    </div>
  )
}
