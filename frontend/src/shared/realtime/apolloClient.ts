import { ApolloClient, concat, HttpLink, InMemoryCache, split } from '@apollo/client'
import { SetContextLink } from '@apollo/client/link/context'
import { GraphQLWsLink } from '@apollo/client/link/subscriptions'
import { getMainDefinition } from '@apollo/client/utilities'
import { createClient } from 'graphql-ws'
import { getAccessToken } from '../auth/accessTokenStore'
import { runtimeConfig } from '../config/runtimeConfig'
import type { ConnectionStatus } from './connectionStatus'

type StatusListener = (status: ConnectionStatus) => void

const statusListeners = new Set<StatusListener>()

function notifyStatus(status: ConnectionStatus): void {
  for (const listener of statusListeners) {
    listener(status)
  }
}

/** Fires on every status transition of the GraphQL WS transport (the `onReadingStored` channel). */
export function onGraphQlWsStatusChange(listener: StatusListener): () => void {
  statusListeners.add(listener)
  return () => {
    statusListeners.delete(listener)
  }
}

const wsClient = createClient({
  url: runtimeConfig.graphqlWsUrl,

  // Connect at startup rather than on first subscribe: TASK-035's connection-status indicator
  // needs to reflect this channel's real state as soon as the app loads, not only once some
  // future screen mounts an onReadingStored subscription.
  lazy: false,

  // Only attached once TASK-041 populates the store; the Gateway falls back to its own
  // dev-bypass authentication until then (see accessTokenStore.ts).
  connectionParams: () => {
    const token = getAccessToken()
    return token ? { Authorization: `Bearer ${token}` } : {}
  },
  on: {
    // graphql-ws reports which phase a transition belongs to via isRetry/wasRetry, so status
    // derivation needs no extra bookkeeping of its own.
    connecting: (isRetry) => notifyStatus(isRetry ? 'reconnecting' : 'connecting'),
    connected: () => notifyStatus('connected'),
    closed: () => notifyStatus('disconnected'),
  },
})

const wsLink = new GraphQLWsLink(wsClient)

// Read fresh on every request (not once at module load), so a token set after this client is
// constructed - the normal case once TASK-041 adds login - is picked up immediately.
const authLink = new SetContextLink((prevContext) => {
  const token = getAccessToken()
  return token
    ? { headers: { ...prevContext.headers, Authorization: `Bearer ${token}` } }
    : prevContext
})

const httpLink = concat(authLink, new HttpLink({ uri: runtimeConfig.graphqlHttpUrl }))

/**
 * Routes subscription operations over the WS link and everything else (queries/mutations) over
 * plain HTTP - the standard Apollo split, so a single client serves both transports.
 */
const splitLink = split(
  ({ query }) => {
    const definition = getMainDefinition(query)
    return definition.kind === 'OperationDefinition' && definition.operation === 'subscription'
  },
  wsLink,
  httpLink,
)

export const apolloClient = new ApolloClient({
  link: splitLink,
  cache: new InMemoryCache(),
})
