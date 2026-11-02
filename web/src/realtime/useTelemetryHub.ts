import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useEffect } from 'react'
import type { Alert, Device, Reading } from '../api/types'
import { useAppDispatch } from '../store'
import { alertRaised, alertUpdated, fetchAlerts } from '../store/alertsSlice'
import { connectionChanged } from '../store/connectionSlice'
import { deviceUpdated, fetchDevices } from '../store/devicesSlice'
import { readingReceived } from '../store/readingsSlice'

const HUB_PATH = '/hubs/telemetry'
const BASE = import.meta.env.VITE_API_URL ?? ''

/**
 * Keeps one SignalR connection alive for the lifetime of the signed-in session and
 * funnels every server push into the Redux store. After a reconnect the REST snapshots
 * are refetched, because pushes that happened while offline were missed.
 */
export function useTelemetryHub(token: string | null) {
  const dispatch = useAppDispatch()

  useEffect(() => {
    if (!token) return

    const connection = new HubConnectionBuilder()
      .withUrl(`${BASE}${HUB_PATH}`, { accessTokenFactory: () => token })
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('DeviceUpdated', (device: Device) => dispatch(deviceUpdated(device)))
    connection.on('ReadingReceived', (deviceId: string, reading: Reading) =>
      dispatch(readingReceived({ deviceId, reading })),
    )
    connection.on('AlertRaised', (alert: Alert) => dispatch(alertRaised(alert)))
    connection.on('AlertUpdated', (alert: Alert) => dispatch(alertUpdated(alert)))

    connection.onreconnecting((err) => dispatch(connectionChanged({ status: 'reconnecting', error: err?.message })))
    connection.onreconnected(() => {
      dispatch(connectionChanged({ status: 'connected' }))
      void dispatch(fetchDevices())
      void dispatch(fetchAlerts())
    })
    connection.onclose((err) => dispatch(connectionChanged({ status: 'disconnected', error: err?.message })))

    let cancelled = false
    dispatch(connectionChanged({ status: 'connecting' }))
    connection
      .start()
      .then(() => {
        if (!cancelled) dispatch(connectionChanged({ status: 'connected' }))
      })
      .catch((err: unknown) => {
        if (!cancelled) dispatch(connectionChanged({ status: 'disconnected', error: (err as Error).message }))
      })

    return () => {
      cancelled = true
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
      dispatch(connectionChanged({ status: 'disconnected' }))
    }
  }, [token, dispatch])
}
