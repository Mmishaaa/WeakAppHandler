import { ApolloClient, HttpLink, InMemoryCache } from '@apollo/client'

export const createApolloClient = (): ApolloClient =>
  new ApolloClient({
    link: new HttpLink({ uri: '/graphql' }),
    cache: new InMemoryCache(),
  })
