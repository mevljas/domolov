import { defineConfig, globalIgnores } from 'eslint/config'
import js from '@eslint/js'
import pluginVue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'
import prettier from 'eslint-config-prettier/flat'
import globals from 'globals'

export default defineConfig(
  globalIgnores([
    'dist/**',
    'dev-dist/**',
    'coverage/**',
    'playwright-report/**',
    'test-results/**',
    'src/api/schema.d.ts',
  ]),
  js.configs.recommended,
  tseslint.configs.recommended,
  pluginVue.configs['flat/recommended'],
  {
    files: ['**/*.vue'],
    languageOptions: {
      parserOptions: {
        parser: tseslint.parser,
        extraFileExtensions: ['.vue'],
        sourceType: 'module',
      },
    },
  },
  {
    languageOptions: {
      globals: { ...globals.browser },
    },
    rules: {
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      'vue/block-order': ['error', { order: ['script', 'template', 'style'] }],
      'vue/component-api-style': ['error', ['script-setup']],
      'vue/define-macros-order': [
        'error',
        {
          order: ['defineOptions', 'defineProps', 'defineEmits', 'defineModel', 'defineSlots'],
        },
      ],
      'vue/block-lang': ['error', { script: { lang: 'ts' } }],
      'vue/no-unused-refs': 'error',
      // Optional props in type-based defineProps are already `undefined`-typed.
      'vue/require-default-prop': 'off',
      'vue/no-useless-v-bind': 'error',
      'vue/prefer-true-attribute-shorthand': 'error',
    },
  },
  {
    files: ['src/shared/components/ui/**/*.vue'],
    rules: {
      'vue/multi-word-component-names': 'off',
    },
  },
  {
    files: ['*.config.ts', 'tests/e2e/**/*.ts'],
    languageOptions: { globals: { ...globals.node } },
  },
  {
    files: ['src/sw.ts'],
    languageOptions: { globals: { ...globals.serviceworker } },
  },
  {
    files: ['public/**/*.js'],
    languageOptions: { sourceType: 'script', globals: { ...globals.browser } },
  },
  prettier,
)
