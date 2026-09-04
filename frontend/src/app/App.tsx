import { ApolloProvider } from '@apollo/client/react'
import { useEffect } from 'react'
import { Route, BrowserRouter, Routes } from 'react-router-dom'
import { AnnouncerProvider } from '../shared/a11y/AnnouncerProvider'
import { resumeAuthSession } from '../shared/auth/authSessionManager'
import { apolloClient } from '../shared/realtime/apolloClient'
import { AppShell } from './layout/AppShell'
import { AdministrationPage } from './pages/AdministrationPage'
import { AlertsPage } from './pages/AlertsPage'
import { HistoryPage } from './pages/HistoryPage'
import { LoginPage } from './pages/LoginPage'
import { OverviewPage } from './pages/OverviewPage'
import { RequireAdmin } from './RequireAdmin'

function App() {
  // Resumes a session from the httpOnly refresh cookie alone, once per app load - see
  // resumeAuthSession's own doc comment for why this is necessary despite the access token being
  // held only in memory.
  useEffect(() => {
    void resumeAuthSession()
  }, [])

  return (
    <ApolloProvider client={apolloClient}>
      <AnnouncerProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<AppShell />}>
              <Route index element={<OverviewPage />} />
              <Route path="history" element={<HistoryPage />} />
              <Route path="alerts" element={<AlertsPage />} />
              <Route path="login" element={<LoginPage />} />
              <Route
                path="administration"
                element={
                  <RequireAdmin>
                    <AdministrationPage />
                  </RequireAdmin>
                }
              />
            </Route>
          </Routes>
        </BrowserRouter>
      </AnnouncerProvider>
    </ApolloProvider>
  )
}

export default App
