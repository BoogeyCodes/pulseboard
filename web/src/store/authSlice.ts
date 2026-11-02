import { createAsyncThunk, createSlice } from '@reduxjs/toolkit'
import { api, ApiError } from '../api/client'
import type { Role } from '../api/types'

const STORAGE_KEY = 'pulseboard.auth'

export interface AuthState {
  token: string | null
  username: string | null
  role: Role | null
  expiresAt: string | null
  status: 'idle' | 'loading' | 'failed'
  error: string | null
}

function restore(): Pick<AuthState, 'token' | 'username' | 'role' | 'expiresAt'> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return { token: null, username: null, role: null, expiresAt: null }
    const saved = JSON.parse(raw) as { token: string; username: string; role: Role; expiresAt: string }
    if (new Date(saved.expiresAt).getTime() <= Date.now()) return { token: null, username: null, role: null, expiresAt: null }
    return saved
  } catch {
    return { token: null, username: null, role: null, expiresAt: null }
  }
}

function persist(state: AuthState) {
  try {
    if (state.token) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ token: state.token, username: state.username, role: state.role, expiresAt: state.expiresAt }),
      )
    } else {
      localStorage.removeItem(STORAGE_KEY)
    }
  } catch {
    // storage unavailable (private mode, etc.): session simply will not survive a reload
  }
}

export const initialAuthState: AuthState = { ...restore(), status: 'idle', error: null }

export const login = createAsyncThunk(
  'auth/login',
  async ({ username, password }: { username: string; password: string }, { rejectWithValue }) => {
    try {
      return await api.login(username, password)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) return rejectWithValue('Wrong username or password.')
      return rejectWithValue(err instanceof Error ? err.message : 'Could not reach the server.')
    }
  },
)

const authSlice = createSlice({
  name: 'auth',
  initialState: initialAuthState,
  reducers: {
    logout(state) {
      state.token = null
      state.username = null
      state.role = null
      state.expiresAt = null
      state.error = null
      persist(state)
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(login.pending, (state) => {
        state.status = 'loading'
        state.error = null
      })
      .addCase(login.fulfilled, (state, { payload }) => {
        state.status = 'idle'
        state.token = payload.token
        state.username = payload.username
        state.role = payload.role
        state.expiresAt = payload.expiresAt
        persist(state)
      })
      .addCase(login.rejected, (state, action) => {
        state.status = 'failed'
        state.error = (action.payload as string | undefined) ?? 'Login failed.'
      })
  },
})

export const { logout } = authSlice.actions
export default authSlice.reducer
