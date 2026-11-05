import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Dev proxy keeps auth cookies same-origin (refresh token cookie is scoped to /api/v1/auth).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5090',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://localhost:5090',
        changeOrigin: true,
      },
    },
  },
})
