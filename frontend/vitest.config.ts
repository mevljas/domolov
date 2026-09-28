import { fileURLToPath } from 'node:url'
import { configDefaults, defineConfig, mergeConfig } from 'vitest/config'
import viteConfig from './vite.config.ts'

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      globals: false,
      root: fileURLToPath(new URL('./', import.meta.url)),
      include: ['src/**/*.test.ts'],
      exclude: [...configDefaults.exclude, 'tests/e2e/**'],
      setupFiles: ['src/test/setup.ts'],
      restoreMocks: true,
      coverage: {
        provider: 'v8',
        reporter: ['text', 'html', 'lcov'],
        include: ['src/shared/lib/**/*.ts', 'src/**/composables/**/*.ts'],
        exclude: ['src/**/*.test.ts', 'src/**/index.ts'],
        thresholds: {
          lines: 80,
        },
      },
    },
  }),
)
