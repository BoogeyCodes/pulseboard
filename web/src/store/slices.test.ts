import { describe, expect, it } from 'vitest'
import type { Alert, Device, Reading } from '../api/types'
import { alertRaised, alertUpdated, selectOpenAlertCountByDevice, selectOpenAlerts, selectRecentlyResolved } from './alertsSlice'
import { deviceSelected, deviceUpdated, selectDeviceCounts, selectDevices, selectSelectedDevice } from './devicesSlice'
import { makeStore } from './index'
import { MAX_POINTS, readingReceived, selectReadingsFor } from './readingsSlice'

const device = (over: Partial<Device>): Device => ({
  id: 'd1',
  name: 'Device 1',
  site: 'Site A',
  status: 'Online',
  lastSeenAt: '2025-06-01T12:00:00Z',
  cpuPercent: 30,
  temperatureC: 45,
  latencyMs: 60,
  ...over,
})

const alert = (over: Partial<Alert>): Alert => ({
  id: 1,
  deviceId: 'd1',
  kind: 'high-cpu',
  severity: 'Warning',
  message: 'CPU high',
  raisedAt: '2025-06-01T12:00:00Z',
  acknowledgedAt: null,
  acknowledgedBy: null,
  resolvedAt: null,
  resolvedBy: null,
  ...over,
})

describe('devices slice', () => {
  it('upserts devices and orders them worst-first, then by site and name', () => {
    const store = makeStore()
    store.dispatch(deviceUpdated(device({ id: 'b', name: 'Bravo', site: 'Site B', status: 'Online' })))
    store.dispatch(deviceUpdated(device({ id: 'a', name: 'Alpha', site: 'Site A', status: 'Online' })))
    store.dispatch(deviceUpdated(device({ id: 'c', name: 'Charlie', status: 'Offline' })))
    store.dispatch(deviceUpdated(device({ id: 'd', name: 'Delta', status: 'Degraded' })))
    store.dispatch(deviceUpdated(device({ id: 'a', name: 'Alpha', site: 'Site A', status: 'Degraded', cpuPercent: 99 })))

    const ordered = selectDevices(store.getState()).map((d) => d.id)
    expect(ordered).toEqual(['c', 'a', 'd', 'b'])
    expect(selectDeviceCounts(store.getState())).toEqual({ total: 4, online: 1, degraded: 2, offline: 1 })
    expect(store.getState().devices.byId.a.cpuPercent).toBe(99)
  })

  it('tracks the selected device and tolerates it disappearing', () => {
    const store = makeStore()
    store.dispatch(deviceUpdated(device({ id: 'x' })))
    store.dispatch(deviceSelected('x'))
    expect(selectSelectedDevice(store.getState())?.id).toBe('x')
    store.dispatch(deviceSelected(null))
    expect(selectSelectedDevice(store.getState())).toBeNull()
  })
})

describe('alerts slice', () => {
  it('separates open from resolved and sorts open by severity then recency', () => {
    const store = makeStore()
    store.dispatch(alertRaised(alert({ id: 1, severity: 'Warning', raisedAt: '2025-06-01T12:00:00Z' })))
    store.dispatch(alertRaised(alert({ id: 2, severity: 'Critical', raisedAt: '2025-06-01T11:00:00Z', deviceId: 'd2' })))
    store.dispatch(alertRaised(alert({ id: 3, severity: 'Warning', raisedAt: '2025-06-01T12:30:00Z' })))
    store.dispatch(alertRaised(alert({ id: 1 }))) // duplicate push is idempotent

    expect(selectOpenAlerts(store.getState()).map((a) => a.id)).toEqual([2, 3, 1])
    expect(selectOpenAlertCountByDevice(store.getState())).toEqual({ d1: 2, d2: 1 })

    store.dispatch(alertUpdated(alert({ id: 3, resolvedAt: '2025-06-01T12:40:00Z', resolvedBy: 'system' })))

    expect(selectOpenAlerts(store.getState()).map((a) => a.id)).toEqual([2, 1])
    expect(selectRecentlyResolved(store.getState()).map((a) => a.id)).toEqual([3])
  })
})

describe('readings slice', () => {
  const reading = (i: number): Reading => ({
    timestamp: new Date(Date.UTC(2025, 5, 1, 12, 0, i)).toISOString(),
    cpuPercent: i,
    temperatureC: 40,
    latencyMs: 50,
  })

  it('keeps a bounded, de-duplicated series per device', () => {
    const store = makeStore()
    for (let i = 0; i < MAX_POINTS + 25; i++) {
      store.dispatch(readingReceived({ deviceId: 'd1', reading: reading(i) }))
    }
    store.dispatch(readingReceived({ deviceId: 'd1', reading: reading(MAX_POINTS + 24) })) // same timestamp as last

    const series = selectReadingsFor('d1')(store.getState())
    expect(series).toHaveLength(MAX_POINTS)
    expect(series[0].cpuPercent).toBe(25)
    expect(series[series.length - 1].cpuPercent).toBe(MAX_POINTS + 24)
    expect(selectReadingsFor('other')(store.getState())).toEqual([])
  })
})
