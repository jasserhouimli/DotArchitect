import { useState, useEffect } from "react"
import { auth } from "@/api/client"
import { LoginPage } from "@/pages/LoginPage"
import { DashboardPage } from "@/pages/DashboardPage"
import { WorkflowEditorPage } from "@/pages/WorkflowEditorPage"

export default function App() {
  const [user, setUser] = useState<{ id: string; email: string; displayName: string } | null>(null)
  const [selectedWorkflow, setSelectedWorkflow] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    auth.me()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false))
  }, [])

  const handleLogout = async () => {
    await auth.logout()
    setUser(null)
    setSelectedWorkflow(null)
  }

  if (loading) return <div className="min-h-screen flex items-center justify-center">Loading...</div>

  if (!user) return <LoginPage onLogin={() => auth.me().then(setUser)} />

  if (selectedWorkflow) {
    return <WorkflowEditorPage workflowId={selectedWorkflow} onBack={() => setSelectedWorkflow(null)} onLogout={handleLogout} />
  }

  return <DashboardPage user={user} onSelectWorkflow={setSelectedWorkflow} onLogout={handleLogout} />
}
