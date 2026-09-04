import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  // Default './' makes built assets resolve correctly from IIS virtual directories
  // such as https://server/LVBFlowStock/ without hardcoding the folder name.
  const base = env.VITE_BASE_PATH || './';

  return {
    base,
    plugins: [react()],
    server: { port: 5173 }
  };
});
