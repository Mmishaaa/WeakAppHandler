import { Route, BrowserRouter, Routes } from 'react-router-dom'
import { AnnouncerProvider } from '../shared/a11y/AnnouncerProvider'
import { AppShell } from './layout/AppShell'
import { AdministrationPage } from './pages/AdministrationPage'
import { AlertsPage } from './pages/AlertsPage'
import { HistoryPage } from './pages/HistoryPage'
import { OverviewPage } from './pages/OverviewPage'

function App() {
  return (
    <AnnouncerProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<OverviewPage />} />
            <Route path="history" element={<HistoryPage />} />
            <Route path="alerts" element={<AlertsPage />} />
            <Route path="administration" element={<AdministrationPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AnnouncerProvider>
  )
}

export default App
