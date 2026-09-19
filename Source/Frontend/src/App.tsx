import { ApolloProvider } from '@apollo/client/react'
import { useMemo } from 'react'
import { createApolloClient } from './api/apolloClient'
import { Dashboard } from './dashboard/Dashboard'

export const App = () => {
  const client = useMemo(() => createApolloClient(), [])

  return (
    <ApolloProvider client={client}>
      <Dashboard />
    </ApolloProvider>
  )
}
