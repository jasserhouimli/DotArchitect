import { useState, useEffect } from "react"
import { auth } from "@/api/client"
import { LoginPage } from "@/pages/LoginPage"
import { DashboardPage } from "@/pages/DashboardPage"
import { WorkspacePage } from "@/pages/WorkspacePage"

export default function App() {
  const [user, setUser] = useState<{ id: string; email: string } | null>(null)
  const [selectedWorkspace, setSelectedWorkspace] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    auth.me()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="min-h-screen flex items-center justify-center">Loading...</div>

  if (!user) return <LoginPage onLogin={() => auth.me().then(setUser)} />

  if (selectedWorkspace) {
    return <WorkspacePage workspaceId={selectedWorkspace} onBack={() => setSelectedWorkspace(null)} />
  }

  return <DashboardPage onSelectWorkspace={setSelectedWorkspace} />
}
