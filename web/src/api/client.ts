import type { Alert, Device, LoginResponse, Reading } from './types'

const BASE = import.meta.env.VITE_API_URL ?? ''

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, token: string | null, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const response = await fetch(`${BASE}${path}`, { ...init, headers })
  if (!response.ok) {
    throw new ApiError(response.status, await describe(response))
  }
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

async function describe(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { title?: string; detail?: string }
    return body.detail ?? body.title ?? response.statusText
  } catch {
    return response.statusText || `HTTP ${response.status}`
  }
}

export const api = {
  login: (username: string, password: string) =>
    request<LoginResponse>('/api/auth/login', null, { method: 'POST', body: JSON.stringify({ username, password }) }),

  devices: (token: string) => request<Device[]>('/api/devices', token),

  readings: (token: string, deviceId: string, minutes = 15) =>
    request<Reading[]>(`/api/devices/${encodeURIComponent(deviceId)}/readings?minutes=${minutes}`, token),

  alerts: (token: string, { open = true, limit = 100 }: { open?: boolean; limit?: number } = {}) =>
    request<Alert[]>(`/api/alerts?open=${open}&limit=${limit}`, token),

  acknowledge: (token: string, id: number) => request<Alert>(`/api/alerts/${id}/acknowledge`, token, { method: 'POST' }),

  resolve: (token: string, id: number) => request<Alert>(`/api/alerts/${id}/resolve`, token, { method: 'POST' }),
}
