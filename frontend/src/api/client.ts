const API_BASE = '/api/v1';

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? match[2] : null;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getCookie('DotArchitect.Token');
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
  me: () => request<{ id: string; email: string; displayName: string }>('/auth/me'),
  logout: () => request<void>('/auth/logout', { method: 'POST' }),
};

export interface Workspace {
  id: string;
  name: string;
  description: string | null;
  createdAt: string;
  updatedAt: string;
}

export const workspaces = {
  list: () => request<Workspace[]>('/workspaces'),
  get: (id: string) => request<Workspace>(`/workspaces/${id}`),
  create: (data: { name: string; description?: string }) =>
    request<string>('/workspaces', { method: 'POST', body: JSON.stringify(data) }),
  rename: (id: string, data: { name: string; description?: string }) =>
    request<void>(`/workspaces/${id}`, { method: 'PATCH', body: JSON.stringify(data) }),
  delete: (id: string) => request<void>(`/workspaces/${id}`, { method: 'DELETE' }),
};

export interface Analysis {
  id: string;
  workspaceId: string;
  status: number;
  originalFileName: string;
  startedAt: string;
  completedAt: string | null;
  projectCount: number;
  referenceCount: number;
  warningCount: number;
  errorCode: string | null;
}

export const analyses = {
  list: (workspaceId: string) => request<Analysis[]>(`/workspaces/${workspaceId}/analyses`),
  get: (id: string) => request<Analysis>(`/analyses/${id}`),
  upload: async (workspaceId: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    const token = getCookie('DotArchitect.Token');
    const res = await fetch(`${API_BASE}/workspaces/${workspaceId}/analyses`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: formData,
      credentials: 'include',
    });
    if (!res.ok) throw new Error('Upload failed');
    return res.json() as Promise<{ id: string }>;
  },
  projects: (id: string) => request<Array<{ id: string; name: string; projectType: string; relativePath: string }>>(`/analyses/${id}/projects`),
  warnings: (id: string) => request<Array<{ code: string; message: string; relativePath: string | null }>>(`/analyses/${id}/warnings`),
};

export interface GraphNode {
  id: string;
  name: string;
  projectType: string;
  targetFrameworks: string | null;
}

export interface GraphEdge {
  source: string;
  target: string;
}

export const graph = {
  get: (analysisId: string) => request<{ nodes: GraphNode[]; edges: GraphEdge[] }>(`/analyses/${analysisId}/graph`),
  dependencies: (analysisId: string, projectId: string) =>
    request<{ direct: GraphNode[]; transitive: GraphNode[] }>(`/analyses/${analysisId}/projects/${projectId}/dependencies`),
  dependents: (analysisId: string, projectId: string) =>
    request<{ direct: GraphNode[]; transitive: GraphNode[] }>(`/analyses/${analysisId}/projects/${projectId}/dependents`),
  impact: (analysisId: string, projectId: string) =>
    request<{ message: string; impactedProjects: GraphNode[] }>(`/analyses/${analysisId}/projects/${projectId}/impact`),
  cycles: (analysisId: string) =>
    request<{ hasCycles: boolean; cycles: string[][] }>(`/analyses/${analysisId}/cycles`),
};

export interface Design {
  id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  revision: number;
}

export interface DesignFull {
  design: Design;
  projects: Array<{ id: string; name: string; relativePath: string; templateType: string; targetFramework: string }>;
  references: Array<{ id: string; sourceProjectDefinitionId: string; targetProjectDefinitionId: string }>;
  groups: Array<{ id: string; name: string }>;
  layouts: Array<{ id: string; projectDefinitionId: string; x: number; y: number }>;
}

export const designs = {
  list: (workspaceId: string) => request<Design[]>(`/workspaces/${workspaceId}/designs`),
  get: (id: string) => request<DesignFull>(`/designs/${id}`),
  create: (workspaceId: string, data: { name: string }) =>
    request<string>(`/workspaces/${workspaceId}/designs`, { method: 'POST', body: JSON.stringify(data) }),
  addProject: (designId: string, data: { name: string; relativePath: string; templateType: string; targetFramework?: string }) =>
    request<string>(`/designs/${designId}/projects`, { method: 'POST', body: JSON.stringify(data) }),
  removeProject: (designId: string, projectId: string) =>
    request<void>(`/designs/${designId}/projects/${projectId}`, { method: 'DELETE' }),
  addReference: (designId: string, data: { sourceProjectId: string; targetProjectId: string }) =>
    request<string>(`/designs/${designId}/references`, { method: 'POST', body: JSON.stringify(data) }),
  removeReference: (designId: string, referenceId: string) =>
    request<void>(`/designs/${designId}/references/${referenceId}`, { method: 'DELETE' }),
  validate: (designId: string) =>
    request<{ isValid: boolean; errors: string[]; warnings: string[] }>(`/designs/${designId}/validate`, { method: 'POST' }),
  generate: async (designId: string) => {
    const token = getCookie('DotArchitect.Token');
    const res = await fetch(`${API_BASE}/designs/${designId}/generate`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      credentials: 'include',
    });
    if (!res.ok) throw new Error('Generation failed');
    return res.blob();
  },
};
