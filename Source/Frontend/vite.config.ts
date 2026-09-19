import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  const gateway = env.GATEWAY_URL ?? 'http://localhost:5243'
  const processor = env.PROCESSOR_URL ?? 'http://localhost:5242'
  const notifications = env.NOTIFICATIONS_URL ?? 'http://localhost:5244'

  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        '/graphql': { target: gateway, changeOrigin: true },
        '/api': { target: processor, changeOrigin: true },
        '/hubs': { target: notifications, changeOrigin: true, ws: true },
      },
    },
    build: {
      rolldownOptions: {
        output: {
          codeSplitting: {
            groups: [
              { name: 'charts', test: /node_modules[\\/](recharts|d3-|victory-)/ },
              { name: 'data', test: /node_modules[\\/](@apollo|graphql|rxjs|@microsoft)/ },
              { name: 'react', test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/ },
            ],
          },
        },
      },
    },
  }
})
