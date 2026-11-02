export type DeviceStatus = 'Online' | 'Degraded' | 'Offline'
export type AlertSeverity = 'Info' | 'Warning' | 'Critical'
export type Role = 'Viewer' | 'Operator' | 'Admin'

export interface Device {
  id: string
  name: string
  site: string
  status: DeviceStatus
  lastSeenAt: string | null
  cpuPercent: number | null
  temperatureC: number | null
  latencyMs: number | null
}

export interface Reading {
  timestamp: string
  cpuPercent: number
  temperatureC: number
  latencyMs: number
}

export interface Alert {
  id: number
  deviceId: string
  kind: string
  severity: AlertSeverity
  message: string
  raisedAt: string
  acknowledgedAt: string | null
  acknowledgedBy: string | null
  resolvedAt: string | null
  resolvedBy: string | null
}

export interface LoginResponse {
  token: string
  username: string
  role: Role
  expiresAt: string
}

export const canOperate = (role: Role | null): boolean => role === 'Operator' || role === 'Admin'
