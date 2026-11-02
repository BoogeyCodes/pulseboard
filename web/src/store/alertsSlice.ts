import { createAsyncThunk, createSelector, createSlice, type PayloadAction } from '@reduxjs/toolkit'
import { api } from '../api/client'
import type { Alert } from '../api/types'
import type { RootState } from './index'

export interface AlertsState {
  byId: Record<number, Alert>
  status: 'idle' | 'loading' | 'failed'
  pending: Record<number, boolean>
}

export const initialAlertsState: AlertsState = { byId: {}, status: 'idle', pending: {} }

const tokenFrom = (getState: () => unknown): string => {
  const token = (getState() as RootState).auth.token
  if (!token) throw new Error('not signed in')
  return token
}

export const fetchAlerts = createAsyncThunk('alerts/fetch', async (_: void, { getState }) =>
  api.alerts(tokenFrom(getState), { open: false, limit: 100 }),
)

export const acknowledgeAlert = createAsyncThunk('alerts/acknowledge', async (id: number, { getState }) =>
  api.acknowledge(tokenFrom(getState), id),
)

export const resolveAlert = createAsyncThunk('alerts/resolve', async (id: number, { getState }) =>
  api.resolve(tokenFrom(getState), id),
)

const alertsSlice = createSlice({
  name: 'alerts',
  initialState: initialAlertsState,
  reducers: {
    alertRaised(state, { payload }: PayloadAction<Alert>) {
      state.byId[payload.id] = payload
    },
    alertUpdated(state, { payload }: PayloadAction<Alert>) {
      state.byId[payload.id] = payload
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchAlerts.pending, (state) => {
        state.status = 'loading'
      })
      .addCase(fetchAlerts.fulfilled, (state, { payload }) => {
        state.status = 'idle'
        // Merge rather than replace: a live update may have arrived while the fetch was in flight.
        for (const alert of payload) state.byId[alert.id] = alert
      })
      .addCase(fetchAlerts.rejected, (state) => {
        state.status = 'failed'
      })
    for (const thunk of [acknowledgeAlert, resolveAlert]) {
      builder
        .addCase(thunk.pending, (state, { meta }) => {
          state.pending[meta.arg] = true
        })
        .addCase(thunk.fulfilled, (state, { payload, meta }) => {
          delete state.pending[meta.arg]
          state.byId[payload.id] = payload
        })
        .addCase(thunk.rejected, (state, { meta }) => {
          delete state.pending[meta.arg]
        })
    }
  },
})

export const { alertRaised, alertUpdated } = alertsSlice.actions
export default alertsSlice.reducer

const SEVERITY_RANK = { Critical: 0, Warning: 1, Info: 2 } as const

export const selectOpenAlerts = createSelector(
  (state: RootState) => state.alerts.byId,
  (byId) =>
    Object.values(byId)
      .filter((a) => a.resolvedAt === null)
      .sort((a, b) => SEVERITY_RANK[a.severity] - SEVERITY_RANK[b.severity] || b.raisedAt.localeCompare(a.raisedAt)),
)

export const selectRecentlyResolved = createSelector(
  (state: RootState) => state.alerts.byId,
  (byId) =>
    Object.values(byId)
      .filter((a) => a.resolvedAt !== null)
      .sort((a, b) => b.resolvedAt!.localeCompare(a.resolvedAt!))
      .slice(0, 8),
)

export const selectOpenAlertCountByDevice = createSelector(selectOpenAlerts, (alerts) => {
  const counts: Record<string, number> = {}
  for (const a of alerts) counts[a.deviceId] = (counts[a.deviceId] ?? 0) + 1
  return counts
})
