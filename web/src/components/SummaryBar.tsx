import { useAppSelector } from '../store'
import { selectOpenAlerts } from '../store/alertsSlice'
import { selectDeviceCounts } from '../store/devicesSlice'

export function SummaryBar() {
  const counts = useAppSelector(selectDeviceCounts)
  const open = useAppSelector(selectOpenAlerts)
  const critical = open.filter((a) => a.severity === 'Critical').length
  const unacked = open.filter((a) => a.acknowledgedAt === null).length

  const tiles = [
    { label: 'Devices', value: counts.total, tone: '' },
    { label: 'Online', value: counts.online, tone: 'ok' },
    { label: 'Degraded', value: counts.degraded, tone: counts.degraded ? 'warn' : '' },
    { label: 'Offline', value: counts.offline, tone: counts.offline ? 'crit' : '' },
    { label: 'Open alerts', value: open.length, tone: open.length ? 'warn' : '' },
    { label: 'Critical', value: critical, tone: critical ? 'crit' : '' },
    { label: 'Unacknowledged', value: unacked, tone: unacked ? 'warn' : '' },
  ]

  return (
    <section className="summary" aria-label="Fleet summary">
      {tiles.map((t) => (
        <div key={t.label} className={`tile ${t.tone ? `tile--${t.tone}` : ''}`}>
          <span className="tile__value">{t.value}</span>
          <span className="tile__label">{t.label}</span>
        </div>
      ))}
    </section>
  )
}
