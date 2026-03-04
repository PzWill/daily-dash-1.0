import { defineConfig } from 'vite';

export default defineConfig({
    base: './',
    build: {
        outDir: '../data/editor',
        emptyOutDir: true
    }
});
