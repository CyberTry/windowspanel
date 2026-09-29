import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import { fileURLToPath, URL } from 'node:url';

// vite.config.ts — dev server proxies /api & /ws to the .NET Server (127.0.0.1:9720).
// `pnpm build` writes the dist into ../WindowsPanel.Server/wwwroot/, which the
// ASP.NET Core static-files middleware serves at the site root.
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://127.0.0.1:9720',
      '/ws':  { target: 'ws://127.0.0.1:9720', ws: true }
    }
  },
  build: {
    outDir: '../WindowsPanel.Server/wwwroot',
    emptyOutDir: true,
    target: 'es2022'
  }
});