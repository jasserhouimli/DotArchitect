import { useState, useEffect, Component, type ReactNode } from "react"
import { auth } from "@/api/client"
import { LoginPage } from "@/pages/LoginPage"
import { DashboardPage } from "@/pages/DashboardPage"
import { WorkflowEditorPage } from "@/pages/WorkflowEditorPage"

class ErrorBoundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state = { error: null as Error | null }

  static getDerivedStateFromError(error: Error) {
    return { error }
  }

  render() {
    if (this.state.error) {
      return (
        <div className="min-h-screen flex items-center justify-center p-8">
          <div className="max-w-md text-center space-y-3">
            <h2 className="font-semibold">Something went wrong rendering this page</h2>
            <p className="text-sm text-muted-foreground">{this.state.error.message}</p>
            <button className="text-sm underline" onClick={() => window.location.reload()}>Reload the page</button>
          </div>
        </div>
      )
    }
    return this.props.children
  }
}

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
    return (
      <ErrorBoundary key={selectedWorkflow}>
        <WorkflowEditorPage workflowId={selectedWorkflow} onBack={() => setSelectedWorkflow(null)} onLogout={handleLogout} />
      </ErrorBoundary>
    )
  }

  return (
    <ErrorBoundary>
      <DashboardPage user={user} onSelectWorkflow={setSelectedWorkflow} onLogout={handleLogout} />
    </ErrorBoundary>
  )
}
