import { createAsyncThunk, createSelector, createSlice, type PayloadAction } from '@reduxjs/toolkit'
import { api } from '../api/client'
import type { Device } from '../api/types'
import type { RootState } from './index'

export interface DevicesState {
  byId: Record<string, Device>
  selectedId: string | null
  status: 'idle' | 'loading' | 'failed'
}

export const initialDevicesState: DevicesState = { byId: {}, selectedId: null, status: 'idle' }

export const fetchDevices = createAsyncThunk('devices/fetch', async (_: void, { getState }) => {
  const token = (getState() as RootState).auth.token
  if (!token) throw new Error('not signed in')
  return api.devices(token)
})

const devicesSlice = createSlice({
  name: 'devices',
  initialState: initialDevicesState,
  reducers: {
    deviceUpdated(state, { payload }: PayloadAction<Device>) {
      state.byId[payload.id] = payload
    },
    deviceSelected(state, { payload }: PayloadAction<string | null>) {
      state.selectedId = payload
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchDevices.pending, (state) => {
        state.status = 'loading'
      })
      .addCase(fetchDevices.fulfilled, (state, { payload }) => {
        state.status = 'idle'
        state.byId = Object.fromEntries(payload.map((d) => [d.id, d]))
        if (state.selectedId && !state.byId[state.selectedId]) state.selectedId = null
      })
      .addCase(fetchDevices.rejected, (state) => {
        state.status = 'failed'
      })
  },
})

export const { deviceUpdated, deviceSelected } = devicesSlice.actions
export default devicesSlice.reducer

const STATUS_RANK = { Offline: 0, Degraded: 1, Online: 2 } as const

export const selectDevices = createSelector(
  (state: RootState) => state.devices.byId,
  (byId) =>
    Object.values(byId).sort(
      (a, b) =>
        STATUS_RANK[a.status] - STATUS_RANK[b.status] || a.site.localeCompare(b.site) || a.name.localeCompare(b.name),
    ),
)

export const selectSelectedDevice = (state: RootState): Device | null =>
  state.devices.selectedId ? (state.devices.byId[state.devices.selectedId] ?? null) : null

export const selectDeviceCounts = createSelector(selectDevices, (devices) => ({
  total: devices.length,
  online: devices.filter((d) => d.status === 'Online').length,
  degraded: devices.filter((d) => d.status === 'Degraded').length,
  offline: devices.filter((d) => d.status === 'Offline').length,
}))
