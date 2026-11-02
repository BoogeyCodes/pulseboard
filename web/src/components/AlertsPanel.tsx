import { useEffect, useState } from 'react'
import { canOperate, type Alert } from '../api/types'
import { useAppDispatch, useAppSelector } from '../store'
import { acknowledgeAlert, resolveAlert, selectOpenAlerts, selectRecentlyResolved } from '../store/alertsSlice'
import { timeAgo } from '../utils/format'

export function AlertsPanel() {
  const open = useAppSelector(selectOpenAlerts)
  const resolved = useAppSelector(selectRecentlyResolved)
  const role = useAppSelector((s) => s.auth.role)
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 5000)
    return () => clearInterval(id)
  }, [])

  return (
    <aside className="alerts" aria-label="Alerts">
      <h2>
        Open alerts <span className="badge">{open.length}</span>
      </h2>
      {open.length === 0 ? (
        <p className="muted">All clear.</p>
      ) : (
        <ul className="alerts__list">
          {open.map((a) => (
            <AlertRow key={a.id} alert={a} operable={canOperate(role)} now={now} />
          ))}
        </ul>
      )}

      {resolved.length > 0 && (
        <details className="alerts__resolved">
          <summary className="muted">Recently resolved ({resolved.length})</summary>
          <ul className="alerts__list">
            {resolved.map((a) => (
              <li key={a.id} className="alert alert--resolved">
                <div className="alert__head">
                  <span className="muted">{a.deviceId}</span>
                  <span className="muted">resolved by {a.resolvedBy} {timeAgo(a.resolvedAt, now)}</span>
                </div>
                <div className="alert__msg muted">{a.message}</div>
              </li>
            ))}
          </ul>
        </details>
      )}
    </aside>
  )
}

function AlertRow({ alert, operable, now }: { alert: Alert; operable: boolean; now: number }) {
  const dispatch = useAppDispatch()
  const deviceName = useAppSelector((s) => s.devices.byId[alert.deviceId]?.name ?? alert.deviceId)
  const busy = useAppSelector((s) => Boolean(s.alerts.pending[alert.id]))

  return (
    <li className={`alert sev-${alert.severity.toLowerCase()}`}>
      <div className="alert__head">
        <span className={`badge badge--${alert.severity.toLowerCase()}`}>{alert.severity}</span>
        <strong>{deviceName}</strong>
        <span className="muted">{timeAgo(alert.raisedAt, now)}</span>
      </div>
      <div className="alert__msg">{alert.message}</div>
      <div className="alert__foot">
        {alert.acknowledgedAt ? (
          <span className="muted">Acknowledged by {alert.acknowledgedBy}</span>
        ) : (
          <span className="muted">Unacknowledged</span>
        )}
        {operable && (
          <span className="alert__actions">
            {!alert.acknowledgedAt && (
              <button type="button" className="btn btn--small" disabled={busy} onClick={() => dispatch(acknowledgeAlert(alert.id))}>
                Acknowledge
              </button>
            )}
            <button type="button" className="btn btn--small" disabled={busy} onClick={() => dispatch(resolveAlert(alert.id))}>
              Resolve
            </button>
          </span>
        )}
      </div>
    </li>
  )
}
