import * as signalR from "@microsoft/signalr"
import type { WorkflowRun, TaskRun, NotificationItem } from "@/api/client"

export interface RunLog {
  id: string
  taskRunId: string | null
  message: string
  level: string
  timestamp: string
}

interface RunEvents {
  onRun: (run: WorkflowRun) => void
  onTask: (task: TaskRun) => void
  onLog: (log: RunLog) => void
  /** Fired after a transport reconnect so the caller can refetch a snapshot. */
  onReconnect: () => void
}

function readToken(): string | undefined {
  const match = document.cookie.match(new RegExp("(^| )Reflow.Token=([^;]+)"))
  return match ? decodeURIComponent(match[2]) : undefined
}

let connection: signalR.HubConnection | null = null
let started: Promise<void> | null = null

function getConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/runs", {
        accessTokenFactory: () => readToken() ?? "",
      })
      .withAutomaticReconnect()
      .build()
  }
  return connection
}

async function ensureStarted(): Promise<signalR.HubConnection> {
  const conn = getConnection()
  if (conn.state === signalR.HubConnectionState.Connected) return conn
  if (!started) {
    started = conn.start().finally(() => {
      started = null
    })
  }
  await started
  return conn
}

export async function subscribeToRun(runId: string, events: RunEvents): Promise<() => void> {
  const conn = await ensureStarted()

  const runHandler = (run: WorkflowRun) => {
    if (run.id === runId) events.onRun(run)
  }
  const taskHandler = (task: TaskRun) => {
    if (task.workflowRunId === runId) events.onTask(task)
  }
  const logHandler = (log: RunLog) => {
    events.onLog(log)
  }
  // Group membership is per-connection: after a transport reconnect we hold a
  // new connection ID and must rejoin, then refetch to close any gap.
  let disposed = false
  const reconnectHandler = () => {
    if (disposed) return
    conn.invoke("JoinRun", runId).then(() => {
      if (!disposed) events.onReconnect()
    }).catch(() => {})
  }

  conn.on("runUpdated", runHandler)
  conn.on("taskUpdated", taskHandler)
  conn.on("logAppended", logHandler)
  conn.onreconnected(reconnectHandler)

  // Handlers are attached before joining, so events arriving after the join
  // are applied on top of whatever snapshot the caller fetches next.
  await conn.invoke("JoinRun", runId)

  return () => {
    if (disposed) return
    disposed = true
    conn.off("runUpdated", runHandler)
    conn.off("taskUpdated", taskHandler)
    conn.off("logAppended", logHandler)
    conn.invoke("LeaveRun", runId).catch(() => {})
  }
}

export async function subscribeToNotifications(
  onNotification: (n: NotificationItem) => void,
  onReconnect: () => void,
): Promise<() => void> {
  const conn = await ensureStarted()

  const handler = (n: NotificationItem) => onNotification(n)
  let disposed = false
  const reconnectHandler = () => {
    if (!disposed) onReconnect()
  }

  conn.on("notificationReceived", handler)
  conn.onreconnected(reconnectHandler)

  return () => {
    if (disposed) return
    disposed = true
    conn.off("notificationReceived", handler)
  }
}
