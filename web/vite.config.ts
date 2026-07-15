import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const API_ORIGIN = 'http://localhost:5124'

// The API is proxied rather than called cross-origin so the app serves from a
// single origin in dev: no CORS configuration, and no HTTPS dev-cert prompt.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: API_ORIGIN, changeOrigin: true },
      '/hubs': { target: API_ORIGIN, changeOrigin: true, ws: true },
    },
  },
})
