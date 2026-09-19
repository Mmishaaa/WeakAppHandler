import type { CodegenConfig } from '@graphql-codegen/cli'

const config: CodegenConfig = {
  schema: '../GraphQlGateway/schema.graphql',
  documents: ['src/**/*.graphql'],
  generates: {
    './src/gql/graphql.ts': {
      plugins: ['typescript-operations', 'typed-document-node'],
      config: {
        useTypeImports: true,
        avoidOptionals: { field: true, inputValue: false },
        enumsAsConst: false,
        scalars: {
          DateTime: 'string',
          Decimal: 'number',
          Long: 'number',
          UUID: 'string',
        },
      },
    },
  },
}

export default config
