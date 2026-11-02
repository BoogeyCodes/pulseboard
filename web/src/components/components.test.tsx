import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Provider } from 'react-redux'
import { describe, expect, it, vi } from 'vitest'
import type { Alert, Device } from '../api/types'
import { makeStore, type RootState } from '../store'
import { AlertsPanel } from './AlertsPanel'
import { DeviceCard } from './DeviceCard'
import { SummaryBar } from './SummaryBar'

const device: Device = {
  id: 'plc-01',
  name: 'Conveyor PLC',
  site: 'Assembly Hall',
  status: 'Degraded',
  lastSeenAt: new Date(Date.now() - 12_000).toISOString(),
  cpuPercent: 91.4,
  temperatureC: 61.26,
  latencyMs: 88,
}

const alert: Alert = {
  id: 7,
  deviceId: 'plc-01',
  kind: 'high-cpu',
  severity: 'Critical',
  message: 'CPU at 96%',
  raisedAt: new Date(Date.now() - 90_000).toISOString(),
  acknowledgedAt: null,
  acknowledgedBy: null,
  resolvedAt: null,
  resolvedBy: null,
}

function storeWith(role: RootState['auth']['role']) {
  return makeStore({
    auth: { token: 't', username: role?.toLowerCase() ?? null, role, expiresAt: null, status: 'idle', error: null },
    devices: { byId: { [device.id]: device }, selectedId: null, status: 'idle' },
    alerts: { byId: { [alert.id]: alert }, status: 'idle', pending: {} },
  })
}

describe('DeviceCard', () => {
  it('shows status, metrics and alert badge, and reports selection', async () => {
    const onSelect = vi.fn()
    render(<DeviceCard device={device} openAlerts={2} selected={false} onSelect={onSelect} />)

    expect(screen.getByRole('button', { name: /Conveyor PLC, Degraded/ })).toBeInTheDocument()
    expect(screen.getByText('91%')).toBeInTheDocument()
    expect(screen.getByText('61.3°C')).toBeInTheDocument()
    expect(screen.getByText('88 ms')).toBeInTheDocument()
    expect(screen.getByTitle('2 open alert(s)')).toHaveTextContent('2')
    expect(screen.getByText(/seen 12s ago/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button'))
    expect(onSelect).toHaveBeenCalledWith('plc-01')
  })
})

describe('AlertsPanel', () => {
  it('hides operator actions from viewers', () => {
    render(
      <Provider store={storeWith('Viewer')}>
        <AlertsPanel />
      </Provider>,
    )

    expect(screen.getByText('CPU at 96%')).toBeInTheDocument()
    expect(screen.getByText('Conveyor PLC')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Resolve' })).not.toBeInTheDocument()
  })

  it('lets operators acknowledge, calling the API with the bearer token', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ...alert, acknowledgedAt: new Date().toISOString(), acknowledgedBy: 'operator' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    render(
      <Provider store={storeWith('Operator')}>
        <AlertsPanel />
      </Provider>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Acknowledge' }))

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/alerts/7/acknowledge')
    expect(init.method).toBe('POST')
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer t')
    expect(await screen.findByText('Acknowledged by operator')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
    vi.unstubAllGlobals()
  })
})

describe('SummaryBar', () => {
  it('derives tiles from the store', () => {
    render(
      <Provider store={storeWith('Viewer')}>
        <SummaryBar />
      </Provider>,
    )

    const tile = (label: string) => screen.getByText(label).previousSibling?.textContent
    expect(tile('Devices')).toBe('1')
    expect(tile('Degraded')).toBe('1')
    expect(tile('Open alerts')).toBe('1')
    expect(tile('Critical')).toBe('1')
    expect(tile('Unacknowledged')).toBe('1')
  })
})
