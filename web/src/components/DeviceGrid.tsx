import { useEffect, useState } from 'react'
import { useAppDispatch, useAppSelector } from '../store'
import { selectOpenAlertCountByDevice } from '../store/alertsSlice'
import { deviceSelected, selectDevices } from '../store/devicesSlice'
import { DeviceCard } from './DeviceCard'

export function DeviceGrid() {
  const dispatch = useAppDispatch()
  const devices = useAppSelector(selectDevices)
  const selectedId = useAppSelector((s) => s.devices.selectedId)
  const alertCounts = useAppSelector(selectOpenAlertCountByDevice)
  const status = useAppSelector((s) => s.devices.status)

  // Re-render "seen Ns ago" labels without waiting for a telemetry push.
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(id)
  }, [])

  if (status === 'loading' && devices.length === 0) return <p className="muted">Loading devices…</p>
  if (status === 'failed' && devices.length === 0) return <p className="error">Could not load devices.</p>

  return (
    <section className="devices" aria-label="Devices">
      {devices.map((d) => (
        <DeviceCard
          key={d.id}
          device={d}
          openAlerts={alertCounts[d.id] ?? 0}
          selected={d.id === selectedId}
          onSelect={(id) => dispatch(deviceSelected(id === selectedId ? null : id))}
          now={now}
        />
      ))}
    </section>
  )
}
