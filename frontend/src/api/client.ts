const API_BASE = '/api/v1';

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? match[2] : null;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getCookie('Reflow.Token');
  const headers: Record<string, string> = {
    ...options.headers as Record<string, string>,
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
    credentials: 'include',
  });

  if (!res.ok) {
    const error = await res.json().catch(() => ({ error: 'Request failed' }));
    throw new Error(error.error || `HTTP ${res.status}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

export const auth = {
  register: (data: { email: string; password: string; displayName: string }) =>
    request<{ id: string }>('/auth/register', { method: 'POST', body: JSON.stringify(data) }),
  login: (data: { email: string; password: string }) =>
    request<{ id: string }>('/auth/login', { method: 'POST', body: JSON.stringify(data) }),
  me: () => request<{ id: string; email: string; displayName: string; createdAt: string }>('/auth/me'),
  logout: () => request<void>('/auth/logout', { method: 'POST' }),
};

export interface Workflow {
  id: string;
  name: string;
  description: string | null;
  status: string;
  currentVersion: number;
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowNode {
  nodeId: string;
  nodeType: string;
  configJson: string | null;
  label: string | null;
  positionX: number;
  positionY: number;
}

export interface WorkflowEdge {
  sourceNodeId: string;
  targetNodeId: string;
}

export interface WorkflowDetail extends Workflow {
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
}

export interface WorkflowVersion {
  id: string;
  versionNumber: number;
  publishedAt: string;
  publishedBy: string;
}

export interface WorkflowVersionDetail extends WorkflowVersion {
  workflowId: string;
  definitionJson: string;
}

export const workflows = {
  list: () => request<Workflow[]>('/workflows'),
  get: (id: string) => request<WorkflowDetail>(`/workflows/${id}`),
  create: async (data: { name: string; description?: string }) => {
    const res = await request<{ id: string }>('/workflows', { method: 'POST', body: JSON.stringify(data) });
    return res.id;
  },
  update: (id: string, data: { name?: string; description?: string; nodes?: WorkflowNode[]; edges?: WorkflowEdge[] }) =>
    request<void>(`/workflows/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  delete: (id: string) => request<void>(`/workflows/${id}`, { method: 'DELETE' }),
  publish: (id: string) => request<{ version: number }>(`/workflows/${id}/publish`, { method: 'POST' }),
  validate: (id: string) => request<{ isValid: boolean; errors: string[]; warnings: string[] }>(`/workflows/${id}/validate`, { method: 'POST' }),
  archive: (id: string) => request<{ message: string }>(`/workflows/${id}/archive`, { method: 'POST' }),
  versions: (id: string) => request<WorkflowVersion[]>(`/workflows/${id}/versions`),
  version: (id: string, n: number) => request<WorkflowVersionDetail>(`/workflows/${id}/versions/${n}`),
  uploadFile: (id: string, file: File) => {
    const form = new FormData()
    form.append("file", file)
    return request<WorkflowFile>(`/workflows/${id}/files`, { method: 'POST', body: form })
  },
  listFiles: (id: string) => request<WorkflowFile[]>(`/workflows/${id}/files`),
  deleteFile: (id: string, fileId: string) => request<void>(`/workflows/${id}/files/${fileId}`, { method: 'DELETE' }),
};

export interface WorkflowFile {
  fileId: string; fileName: string; size: number; rows: number; columns: string[]; uploadedAt: string;
}

export interface WorkflowRun {
  id: string; workflowId: string; versionNumber: number; status: number;
  createdAt: string; startedAt: string | null; completedAt: string | null; error: string | null;
  totalTasks: number; completedTasks: number; failedTasks: number;
  triggerKind: string; triggerName: string | null;
}
export interface TaskRun {
  id: string; workflowRunId: string; nodeId: string; nodeType: string; status: number; error: string | null;
  createdAt: string; startedAt: string | null; completedAt: string | null;
  attemptCount: number; outputSummary: string | null; rowCount: number | null;
}
export interface TaskDetail extends TaskRun { configJson: string | null; outputJson: string | null; }
export interface Attempt {
  id: string; attemptNumber: number; status: number;
  startedAt: string; completedAt: string | null; error: string | null; log: string | null;
}

export const runs = {
  start: async (workflowId: string) => {
    const res = await request<{ id: string }>(`/workflows/${workflowId}/runs`, { method: 'POST' });
    return res.id;
  },
  list: (workflowId: string) => request<WorkflowRun[]>(`/workflows/${workflowId}/runs`),
  get: (runId: string) => request<WorkflowRun>(`/runs/${runId}`),
  tasks: (runId: string) => request<TaskRun[]>(`/runs/${runId}/tasks`),
  logs: (runId: string) => request<Array<{ id: string; taskRunId: string | null; message: string; level: string; timestamp: string }>>(`/runs/${runId}/logs`),
  cancel: (runId: string) => request<{ message: string }>(`/runs/${runId}/cancel`, { method: 'POST' }),
};

export const tasks = {
  get: (taskId: string) => request<TaskDetail>(`/tasks/${taskId}`),
  attempts: (taskId: string) => request<Attempt[]>(`/tasks/${taskId}/attempts`),
  retry: (taskId: string) => request<{ message: string }>(`/tasks/${taskId}/retry`, { method: 'POST' }),
};

export interface NotificationRule {
  workflowId: string; notifyOnSuccess: boolean; notifyOnFailure: boolean;
  rejectsAbove: number | null; webhookUrl: string | null; updatedAt: string;
}

export interface NotificationItem {
  id: string; workflowId: string; workflowRunId: string; kind: number;
  title: string; message: string; isRead: boolean; createdAt: string;
}

export const notifications = {
  getRule: (workflowId: string) => request<NotificationRule>(`/workflows/${workflowId}/notifications/rule`),
  saveRule: (workflowId: string, data: { notifyOnSuccess: boolean; notifyOnFailure: boolean; rejectsAbove?: number | null; webhookUrl?: string | null }) =>
    request<NotificationRule>(`/workflows/${workflowId}/notifications/rule`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteRule: (workflowId: string) => request<void>(`/workflows/${workflowId}/notifications/rule`, { method: 'DELETE' }),
  list: (unreadOnly = false) => request<NotificationItem[]>(`/notifications?unreadOnly=${unreadOnly}`),
  unreadCount: async () => {
    const res = await request<{ unread: number }>('/notifications/unread-count');
    return res.unread;
  },
  markRead: (id: string) => request<void>(`/notifications/${id}/read`, { method: 'POST' }),
  markAllRead: () => request<{ marked: number }>('/notifications/read-all', { method: 'POST' }),
};

export const NOTIFICATION_KINDS = ['RunSucceeded', 'RunFailed', 'RejectsAboveThreshold'];

export interface TriggerItem {
  id: string; workflowId: string; kind: number; name: string; isEnabled: boolean;
  cronExpression: string | null; timezone: string | null; overlapPolicy: number;
  nextRunAt: string | null; lastFiredAt: string | null;
  createdAt: string; updatedAt: string;
}

export interface WebhookCreated {
  id: string; name: string; url: string;
}

export const triggers = {
  list: (workflowId: string) => request<TriggerItem[]>(`/workflows/${workflowId}/triggers`),
  createSchedule: (workflowId: string, data: { name: string; cronExpression: string; timezone: string; overlapPolicy: number; isEnabled: boolean }) =>
    request<TriggerItem>(`/workflows/${workflowId}/triggers/schedules`, { method: 'POST', body: JSON.stringify(data) }),
  updateSchedule: (workflowId: string, triggerId: string, data: { name?: string; cronExpression?: string; timezone?: string; overlapPolicy?: number; isEnabled?: boolean }) =>
    request<TriggerItem>(`/workflows/${workflowId}/triggers/schedules/${triggerId}`, { method: 'PUT', body: JSON.stringify(data) }),
  createWebhook: (workflowId: string, data: { name: string }) =>
    request<WebhookCreated>(`/workflows/${workflowId}/triggers/webhooks`, { method: 'POST', body: JSON.stringify(data) }),
  regenerateWebhook: (workflowId: string, triggerId: string) =>
    request<WebhookCreated>(`/workflows/${workflowId}/triggers/webhooks/${triggerId}/regenerate`, { method: 'POST' }),
  delete: (workflowId: string, triggerId: string) =>
    request<void>(`/workflows/${workflowId}/triggers/${triggerId}`, { method: 'DELETE' }),
};

export const artifactUrl = (runId: string, nodeId: string) => `/api/v1/runs/${runId}/artifacts/${encodeURIComponent(nodeId)}`;

export const RUN_STATUSES = ['Queued', 'Running', 'Completed', 'Failed', 'Cancelled'];
export const TASK_STATUSES = ['Pending', 'Ready', 'Running', 'Completed', 'Failed', 'RetryScheduled', 'Cancelled', 'Skipped'];
