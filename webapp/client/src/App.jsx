import { useState } from 'react'
import Dashboard from './pages/Dashboard'
import JobSearch from './pages/JobSearch'
import ImportLinks from './pages/ImportLinks'
import UsefulLinks from './pages/UsefulLinks'
import Kanban from './pages/Kanban'
import AddJob from './pages/AddJob'
import Settings from './pages/Settings'
import PromptsLog from './pages/PromptsLog'
import CodeStats from './pages/CodeStats'
import './App.css'

export default function App() {
  const [page, setPage]         = useState('dashboard')
  const [anonymize, setAnonymize] = useState(false)

  return (
    <div className="app">
      <nav className="app-nav">
        <span className="nav-brand">Job Search DB</span>
        <button className={page === 'dashboard' ? 'nav-link active' : 'nav-link'} onClick={() => setPage('dashboard')}>Dashboard</button>
        <button className={page === 'search'    ? 'nav-link active' : 'nav-link'} onClick={() => setPage('search')}>Search Results</button>
        <button className={page === 'kanban'    ? 'nav-link active' : 'nav-link'} onClick={() => setPage('kanban')}>Pipeline</button>
        <button className={page === 'add'       ? 'nav-link active' : 'nav-link'} onClick={() => setPage('add')}>Add Job</button>
        <button className={page === 'import'    ? 'nav-link active' : 'nav-link'} onClick={() => setPage('import')}>Import Links</button>
        <button className={page === 'useful'    ? 'nav-link active' : 'nav-link'} onClick={() => setPage('useful')}>Useful Links</button>
        <button className={page === 'prompts'   ? 'nav-link active' : 'nav-link'} onClick={() => setPage('prompts')}>Prompts Log</button>
        <button className={page === 'settings'  ? 'nav-link active' : 'nav-link'} onClick={() => setPage('settings')}>Settings</button>
        <button className={page === 'codestats' ? 'nav-link active' : 'nav-link'} onClick={() => setPage('codestats')}>Code Stats</button>
        <label className="anon-toggle nav-anon">
          <input type="checkbox" checked={anonymize} onChange={e => setAnonymize(e.target.checked)} />
          <span>Anonymize</span>
        </label>
      </nav>

      {page === 'dashboard' && <Dashboard anonymize={anonymize} />}
      {page === 'search' && <JobSearch anonymize={anonymize} setAnonymize={setAnonymize} />}
      {page === 'kanban' && <Kanban anonymize={anonymize} />}
      {page === 'add'    && <AddJob />}
      {page === 'import' && <ImportLinks />}
      {page === 'useful' && <UsefulLinks />}
      {page === 'prompts' && <PromptsLog />}
      {page === 'settings' && <Settings />}
      {page === 'codestats' && <CodeStats />}
    </div>
  )
}
