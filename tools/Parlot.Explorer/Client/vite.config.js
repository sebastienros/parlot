import { defineConfig } from 'vite';
import { copyFile, mkdir } from 'node:fs/promises';
export default defineConfig({
  base: './',
  build: { outDir: '../wwwroot', emptyOutDir: true, chunkSizeWarningLimit: 3000 },
  plugins: [{
    name: 'bundled-licenses',
    async closeBundle() {
      await mkdir('../wwwroot/licenses', { recursive: true });
      await Promise.all([
        copyFile('node_modules/monaco-editor/LICENSE', '../wwwroot/licenses/monaco.txt'),
        copyFile('node_modules/monaco-editor/ThirdPartyNotices.txt', '../wwwroot/licenses/monaco-third-party.txt'),
        copyFile('node_modules/preact/LICENSE', '../wwwroot/licenses/preact.txt'),
      ]);
    },
  }],
});
