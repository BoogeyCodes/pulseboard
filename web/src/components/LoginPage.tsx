import { useState, type FormEvent } from 'react'
import { useAppDispatch, useAppSelector } from '../store'
import { login } from '../store/authSlice'

const DEMO_ACCOUNTS = [
  { username: 'viewer', role: 'Viewer', can: 'read-only' },
  { username: 'operator', role: 'Operator', can: 'acknowledge & resolve' },
  { username: 'admin', role: 'Admin', can: 'everything' },
]

export function LoginPage() {
  const dispatch = useAppDispatch()
  const { status, error } = useAppSelector((s) => s.auth)
  const [username, setUsername] = useState('operator')
  const [password, setPassword] = useState('pulse123')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    void dispatch(login({ username, password }))
  }

  return (
    <main className="login">
      <form className="login__card" onSubmit={submit} aria-labelledby="login-title">
        <div className="brand">
          <span className="brand__dot" aria-hidden />
          <h1 id="login-title">PulseBoard</h1>
        </div>
        <p className="muted">Sign in to the fleet monitoring console.</p>

        <label>
          Username
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" required />
        </label>
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}

        <button type="submit" className="btn btn--primary" disabled={status === 'loading'}>
          {status === 'loading' ? 'Signing in…' : 'Sign in'}
        </button>

        <div className="login__demo">
          <p className="muted">Demo accounts (password <code>pulse123</code>):</p>
          <ul>
            {DEMO_ACCOUNTS.map((a) => (
              <li key={a.username}>
                <button type="button" className="link" onClick={() => setUsername(a.username)}>
                  {a.username}
                </button>{' '}
                <span className="muted">
                  {a.role}, {a.can}
                </span>
              </li>
            ))}
          </ul>
        </div>
      </form>
    </main>
  )
}
