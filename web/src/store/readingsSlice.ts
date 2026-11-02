import { createAsyncThunk, createSlice, type PayloadAction } from '@reduxjs/toolkit'
import { api } from '../api/client'
import type { Reading } from '../api/types'
import type { RootState } from './index'

/** Samples kept per device in memory. At a 2 s interval this is ten minutes of history. */
export const MAX_POINTS = 300

export interface ReadingsState {
  byDevice: Record<string, Reading[]>
  loading: Record<string, boolean>
}

export const initialReadingsState: ReadingsState = { byDevice: {}, loading: {} }

export const fetchReadings = createAsyncThunk('readings/fetch', async (deviceId: string, { getState }) => {
  const token = (getState() as RootState).auth.token
  if (!token) throw new Error('not signed in')
  return { deviceId, readings: await api.readings(token, deviceId, 15) }
})

const readingsSlice = createSlice({
  name: 'readings',
  initialState: initialReadingsState,
  reducers: {
    readingReceived(state, { payload }: PayloadAction<{ deviceId: string; reading: Reading }>) {
      const series = (state.byDevice[payload.deviceId] ??= [])
      const last = series[series.length - 1]
      if (last && last.timestamp === payload.reading.timestamp) return
      series.push(payload.reading)
      if (series.length > MAX_POINTS) series.splice(0, series.length - MAX_POINTS)
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchReadings.pending, (state, { meta }) => {
        state.loading[meta.arg] = true
      })
      .addCase(fetchReadings.fulfilled, (state, { payload }) => {
        delete state.loading[payload.deviceId]
        // History from the API plus anything that streamed in after the request's snapshot.
        const live = state.byDevice[payload.deviceId] ?? []
        const lastHistorical = payload.readings[payload.readings.length - 1]?.timestamp ?? ''
        const merged = [...payload.readings, ...live.filter((r) => r.timestamp > lastHistorical)]
        state.byDevice[payload.deviceId] = merged.slice(-MAX_POINTS)
      })
      .addCase(fetchReadings.rejected, (state, { meta }) => {
        delete state.loading[meta.arg]
      })
  },
})

export const { readingReceived } = readingsSlice.actions
export default readingsSlice.reducer

export const selectReadingsFor = (deviceId: string | null) => (state: RootState) =>
  deviceId ? (state.readings.byDevice[deviceId] ?? EMPTY) : EMPTY

const EMPTY: Reading[] = []
