import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import { serviceWorker } from './pwa/servicePlugin';

const path = (relative: string) => fileURLToPath(new URL(relative, import.meta.url));

// Serwer podaje zbudowany interfejs z wwwroot, więc aplikacja i API mają jeden adres.
export default defineConfig({
  plugins: [react(), serviceWorker(path('./pwa/sw.template.js'), path('./public'))],
  build: {
    outDir: '../Jajcarz.Server/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
});
