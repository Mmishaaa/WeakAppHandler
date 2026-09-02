import type { CodegenConfig } from '@graphql-codegen/cli'

const config: CodegenConfig = {
  schema: '../src/Gateway/WeakAppHandler.Gateway.Api/schema.graphql',
  documents: ['src/**/*.{ts,tsx}'],
  ignoreNoDocuments: true,
  generates: {
    'src/gql/': {
      preset: 'client',
      // Matches this scaffold's own tsconfig strictness: verbatimModuleSyntax needs every
      // type-only import written as `import type`, and erasableSyntaxOnly rejects a real `enum`
      // declaration (TypeScript can't erase it without a runtime object) - without these, the
      // generated output fails tsc the moment anything imports it, which defeats the point of
      // committing a real, buildable scaffold.
      config: {
        useTypeImports: true,
        enumsAsTypes: true,
      },
    },
  },
}

export default config
