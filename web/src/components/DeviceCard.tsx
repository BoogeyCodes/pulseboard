import type { Device } from '../api/types'
import { metric, timeAgo } from '../utils/format'

interface Props {
  device: Device
  openAlerts: number
  selected: boolean
  onSelect: (id: string) => void
  now?: number
}

export function DeviceCard({ device, openAlerts, selected, onSelect, now }: Props) {
  return (
    <button
      type="button"
      className={`device status-${device.status.toLowerCase()} ${selected ? 'device--selected' : ''}`}
      onClick={() => onSelect(device.id)}
      aria-pressed={selected}
      aria-label={`${device.name}, ${device.status}`}
    >
      <div className="device__head">
        <span className="status-dot" aria-hidden />
        <span className="device__name">{device.name}</span>
        {openAlerts > 0 && (
          <span className="badge badge--alert" title={`${openAlerts} open alert(s)`}>
            {openAlerts}
          </span>
        )}
      </div>
      <div className="device__meta muted">
        {device.site} · {device.status} · seen {timeAgo(device.lastSeenAt, now)}
      </div>
      <dl className="device__metrics">
        <div>
          <dt>CPU</dt>
          <dd>{metric(device.cpuPercent, 0, '%')}</dd>
        </div>
        <div>
          <dt>Temp</dt>
          <dd>{metric(device.temperatureC, 1, '°C')}</dd>
        </div>
        <div>
          <dt>Latency</dt>
          <dd>{metric(device.latencyMs, 0, ' ms')}</dd>
        </div>
      </dl>
    </button>
  )
}
