import js from '@eslint/js'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import { globalIgnores } from 'eslint/config'
import globals from 'globals'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  globalIgnores(['dist', 'src/gql']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      // eslint-plugin-react-hooks 7's top-level configs['recommended-latest'] still shapes
      // `plugins` as a string array (the pre-flat-config convention) - ESLint 10 rejects that
      // outright rather than auto-converting it. `configs.flat['recommended-latest']` is the
      // actual flat-config-shaped equivalent (an object keyed by plugin name).
      reactHooks.configs.flat['recommended-latest'],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
  },
)
