import { useEffect } from 'react'
import { Line, LineChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { Reading } from '../api/types'
import { useAppDispatch, useAppSelector } from '../store'
import { selectSelectedDevice } from '../store/devicesSlice'
import { fetchReadings, selectReadingsFor } from '../store/readingsSlice'
import { clock } from '../utils/format'

const THRESHOLDS = { cpuPercent: 85, temperatureC: 80, latencyMs: 400 }

export function DeviceDetail() {
  const dispatch = useAppDispatch()
  const device = useAppSelector(selectSelectedDevice)
  const readings = useAppSelector(selectReadingsFor(device?.id ?? null))
  const loading = useAppSelector((s) => (device ? Boolean(s.readings.loading[device.id]) : false))

  useEffect(() => {
    if (device) void dispatch(fetchReadings(device.id))
  }, [device?.id, dispatch]) // eslint-disable-line react-hooks/exhaustive-deps

  if (!device) {
    return (
      <section className="detail detail--empty" aria-label="Device detail">
        <p className="muted">Select a device to see its live telemetry.</p>
      </section>
    )
  }

  return (
    <section className="detail" aria-label={`${device.name} telemetry`}>
      <header className="detail__head">
        <h2>{device.name}</h2>
        <span className="muted">
          {device.site} · {device.id} · {readings.length} samples{loading ? ' · loading…' : ''}
        </span>
      </header>
      <div className="charts">
        <Chart title="CPU %" data={readings} field="cpuPercent" color="var(--c-cpu)" max={100} />
        <Chart title="Temperature °C" data={readings} field="temperatureC" color="var(--c-temp)" />
        <Chart title="Latency ms" data={readings} field="latencyMs" color="var(--c-lat)" />
      </div>
    </section>
  )
}

interface ChartProps {
  title: string
  data: Reading[]
  field: keyof typeof THRESHOLDS
  color: string
  max?: number
}

function Chart({ title, data, field, color, max }: ChartProps) {
  return (
    <figure className="chart">
      <figcaption>{title}</figcaption>
      <ResponsiveContainer width="100%" height={140}>
        <LineChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <XAxis dataKey="timestamp" tickFormatter={clock} minTickGap={48} stroke="var(--fg-muted)" fontSize={11} />
          <YAxis domain={[0, max ?? 'auto']} width={36} stroke="var(--fg-muted)" fontSize={11} />
          <Tooltip
            labelFormatter={(v) => clock(String(v))}
            contentStyle={{ background: 'var(--bg-elev)', border: '1px solid var(--border)', borderRadius: 6 }}
          />
          <ReferenceLine y={THRESHOLDS[field]} stroke="var(--c-warn)" strokeDasharray="4 4" />
          <Line type="monotone" dataKey={field} stroke={color} dot={false} isAnimationActive={false} strokeWidth={2} />
        </LineChart>
      </ResponsiveContainer>
    </figure>
  )
}
