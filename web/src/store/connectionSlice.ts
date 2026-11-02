import { createSlice, type PayloadAction } from '@reduxjs/toolkit'

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

export interface ConnectionState {
  status: ConnectionStatus
  lastError: string | null
}

export const initialConnectionState: ConnectionState = { status: 'disconnected', lastError: null }

const connectionSlice = createSlice({
  name: 'connection',
  initialState: initialConnectionState,
  reducers: {
    connectionChanged(state, { payload }: PayloadAction<{ status: ConnectionStatus; error?: string | null }>) {
      state.status = payload.status
      state.lastError = payload.error ?? null
    },
  },
})

export const { connectionChanged } = connectionSlice.actions
export default connectionSlice.reducer
