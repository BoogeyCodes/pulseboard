import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

const API = process.env.VITE_API_PROXY ?? 'http://localhost:5100'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: API, changeOrigin: true },
      '/hubs': { target: API, changeOrigin: true, ws: true },
    },
  },
  build: {
    // recharts + signalr + react land around 650 kB minified; acceptable for an internal console.
    chunkSizeWarningLimit: 700,
  },
  test: {
    environment: 'jsdom',
    globals: false,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
