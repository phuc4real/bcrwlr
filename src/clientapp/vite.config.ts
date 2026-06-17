import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// In dev, proxy API calls to the ASP.NET Core backend.
// On build, emit the SPA into the API's wwwroot so the backend serves it in production.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5220',
    },
  },
  build: {
    outDir: '../Bcrwlr.Api/wwwroot',
    emptyOutDir: true,
  },
})
