import { useAppDispatch, useAppSelector } from '../store'
import { logout } from '../store/authSlice'
import type { ConnectionStatus } from '../store/connectionSlice'

const LABEL: Record<ConnectionStatus, string> = {
  connected: 'Live',
  connecting: 'Connecting',
  reconnecting: 'Reconnecting',
  disconnected: 'Offline',
}

export function Header() {
  const dispatch = useAppDispatch()
  const { username, role } = useAppSelector((s) => s.auth)
  const connection = useAppSelector((s) => s.connection.status)

  return (
    <header className="header">
      <div className="brand">
        <span className="brand__dot" aria-hidden />
        <h1>PulseBoard</h1>
      </div>
      <div className="header__right">
        <span className={`pill pill--${connection}`} title={LABEL[connection]} aria-live="polite">
          <span className="pill__dot" aria-hidden /> {LABEL[connection]}
        </span>
        <span className="muted">
          {username} <span className="badge">{role}</span>
        </span>
        <button type="button" className="btn" onClick={() => dispatch(logout())}>
          Sign out
        </button>
      </div>
    </header>
  )
}
