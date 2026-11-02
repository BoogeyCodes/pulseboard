import { useEffect } from 'react'
import { AlertsPanel } from './components/AlertsPanel'
import { DeviceDetail } from './components/DeviceDetail'
import { DeviceGrid } from './components/DeviceGrid'
import { Header } from './components/Header'
import { LoginPage } from './components/LoginPage'
import { SummaryBar } from './components/SummaryBar'
import { useTelemetryHub } from './realtime/useTelemetryHub'
import { useAppDispatch, useAppSelector } from './store'
import { fetchAlerts } from './store/alertsSlice'
import { fetchDevices } from './store/devicesSlice'

export default function App() {
  const token = useAppSelector((s) => s.auth.token)
  return token ? <Dashboard token={token} /> : <LoginPage />
}

function Dashboard({ token }: { token: string }) {
  const dispatch = useAppDispatch()
  useTelemetryHub(token)

  useEffect(() => {
    void dispatch(fetchDevices())
    void dispatch(fetchAlerts())
  }, [dispatch, token])

  return (
    <div className="app">
      <Header />
      <main className="dashboard">
        <SummaryBar />
        <div className="dashboard__body">
          <div className="dashboard__main">
            <DeviceGrid />
            <DeviceDetail />
          </div>
          <AlertsPanel />
        </div>
      </main>
    </div>
  )
}
