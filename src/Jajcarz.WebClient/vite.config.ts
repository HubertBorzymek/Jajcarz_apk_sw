import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Serwer podaje zbudowany interfejs z wwwroot, więc aplikacja i API mają jeden adres.
export default defineConfig({
  plugins: [react()],
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
