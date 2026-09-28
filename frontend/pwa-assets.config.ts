import { defineConfig, minimal2023Preset } from '@vite-pwa/assets-generator/config'

const forest = '#1F4D3A'

export default defineConfig({
  headLinkOptions: {
    preset: '2023',
  },
  preset: {
    ...minimal2023Preset,
    maskable: {
      ...minimal2023Preset.maskable,
      padding: 0.3,
      resizeOptions: { background: forest },
    },
    apple: {
      ...minimal2023Preset.apple,
      padding: 0.3,
      resizeOptions: { background: forest },
    },
  },
  images: ['public/favicon.svg'],
})
