import { fileURLToPath, URL } from 'node:url'
import type { ProxyOptions } from 'vite'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

const backend = process.env.DOMOLOV_API_ORIGIN ?? 'http://localhost:5080'

const backendProxy: ProxyOptions = {
  target: backend,
  changeOrigin: false,
  // Server-Sent Events are long-lived: never time out and never buffer.
  timeout: 0,
  proxyTimeout: 0,
  configure(proxy) {
    proxy.on('proxyRes', (proxyRes) => {
      const type = proxyRes.headers['content-type'] ?? ''
      if (type.includes('text/event-stream')) {
        proxyRes.headers['cache-control'] = 'no-cache, no-transform'
        proxyRes.headers['x-accel-buffering'] = 'no'
        delete proxyRes.headers['content-length']
      }
    })
  },
}

export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
    VitePWA({
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.ts',
      injectRegister: false,
      registerType: 'autoUpdate',
      manifestFilename: 'manifest.webmanifest',
      includeAssets: ['favicon.svg', 'favicon.ico', 'apple-touch-icon-180x180.png'],
      manifest: {
        id: '/',
        name: 'Domolov',
        short_name: 'Domolov',
        description: 'Your personal real-estate hunter.',
        lang: 'sl',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        background_color: '#F7F5F0',
        theme_color: '#1F4D3A',
        icons: [
          { src: 'pwa-64x64.png', sizes: '64x64', type: 'image/png' },
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          {
            src: 'maskable-icon-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
      injectManifest: {
        globPatterns: ['**/*.{js,css,html,svg,png,ico,woff2,webmanifest}'],
        // Only Latin subsets are needed offline (sl + en); others load on demand via unicode-range.
        globIgnores: ['**/*-{cyrillic,cyrillic-ext,greek,greek-ext,vietnamese}-*.woff2'],
      },
      devOptions: { enabled: false, type: 'module' },
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': backendProxy,
      '/health': backendProxy,
    },
  },
  preview: {
    port: 4173,
    strictPort: true,
  },
  build: {
    outDir: 'dist',
    target: 'es2022',
    sourcemap: true,
  },
})
