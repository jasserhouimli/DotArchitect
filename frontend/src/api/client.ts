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
};
