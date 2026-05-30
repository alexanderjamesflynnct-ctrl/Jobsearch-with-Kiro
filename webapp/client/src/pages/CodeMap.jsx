import { useState } from 'react'
import SwaggerDocs from '../components/SwaggerDocs'
import PromptsLog from './PromptsLog'
import CodeStats from './CodeStats'

const API_MAP = [
  { ui: 'Dashboard', uiMethod: 'Dashboard()', action: 'Load stats', controller: 'StatsController', endpoint: '/stats', method: 'GET', sql: 'SELECT', tables: ['job_listings', 'searches'] },
  { ui: 'Dashboard', uiMethod: 'Dashboard()', action: 'Load pipeline cards', controller: 'KanbanController', endpoint: '/kanban', method: 'GET', sql: 'SELECT', tables: ['kanban_jobs', 'job_listings'] },
  { ui: 'Dashboard', uiMethod: 'Dashboard()', action: 'Applied count (all time)', controller: 'StatsController', endpoint: '/stats/applied-count', method: 'GET', sql: 'SELECT', tables: ['kanban_history'] },
  { ui: 'Dashboard', uiMethod: 'Dashboard()', action: 'Applied today count', controller: 'StatsController', endpoint: '/stats/applied-today', method: 'GET', sql: 'SELECT', tables: ['kanban_history', 'settings'] },
  { ui: 'Dashboard', uiMethod: 'PieChart()', action: 'Application outcomes', controller: 'StatsController', endpoint: '/stats/outcomes', method: 'GET', sql: 'SELECT', tables: ['kanban_jobs', 'kanban_history'] },
  { ui: 'Search Results', uiMethod: 'fetchJobs()', action: 'Load jobs (paginated)', controller: 'JobsController', endpoint: '/jobs', method: 'GET', sql: 'SELECT', tables: ['job_listings'] },
  { ui: 'Search Results', uiMethod: 'JobSearch()', action: 'Load filter options', controller: 'JobsController', endpoint: '/countries', method: 'GET', sql: 'SELECT', tables: ['job_listings'] },
  { ui: 'Search Results', uiMethod: 'JobSearch()', action: 'Load sources', controller: 'JobsController', endpoint: '/sources', method: 'GET', sql: 'SELECT', tables: ['job_listings'] },
  { ui: 'Search Results', uiMethod: 'JobSearch()', action: 'Load job types', controller: 'JobsController', endpoint: '/job-types', method: 'GET', sql: 'SELECT', tables: ['job_listings'] },
  { ui: 'Search Results', uiMethod: 'JobSearch()', action: 'Load states', controller: 'JobsController', endpoint: '/states', method: 'GET', sql: 'SELECT', tables: ['job_listings'] },
  { ui: 'Search Results', uiMethod: 'deleteJob()', action: 'Delete a job', controller: 'JobsController', endpoint: '/jobs/{id}', method: 'DELETE', sql: 'DELETE', tables: ['job_listings'] },
  { ui: 'Search Results', uiMethod: 'CountryCell.save()', action: 'Update country', controller: 'JobsController', endpoint: '/jobs/{id}/country', method: 'PATCH', sql: 'UPDATE', tables: ['job_listings'] },
  { ui: 'Search Results', uiMethod: 'runAdzuna()', action: 'Fetch Adzuna jobs', controller: 'AdzunaController', endpoint: '/run-adzuna', method: 'POST', sql: 'INSERT', tables: ['job_listings', 'searches', 'kanban_jobs'] },
  { ui: 'Search Results', uiMethod: 'deleteStale()', action: 'Clear untouched jobs', controller: 'JobsController', endpoint: '/jobs/stale', method: 'DELETE', sql: 'DELETE', tables: ['job_listings', 'kanban_jobs'] },
  { ui: 'Pipeline', uiMethod: 'Kanban()', action: 'Load all kanban cards', controller: 'KanbanController', endpoint: '/kanban', method: 'GET', sql: 'SELECT/INSERT', tables: ['kanban_jobs', 'job_listings'] },
  { ui: 'Pipeline', uiMethod: 'moveCard()', action: 'Move card (change status)', controller: 'KanbanController', endpoint: '/kanban/{id}/status', method: 'PATCH', sql: 'UPDATE/INSERT', tables: ['kanban_jobs', 'kanban_history'] },
  { ui: 'Pipeline', uiMethod: 'setFailType()', action: 'Set fail type', controller: 'KanbanController', endpoint: '/kanban/{id}/fail-type', method: 'PATCH', sql: 'UPDATE', tables: ['kanban_jobs'] },
  { ui: 'Pipeline', uiMethod: 'setActive()', action: 'Toggle active/focus', controller: 'KanbanController', endpoint: '/kanban/{id}/active', method: 'POST', sql: 'UPDATE', tables: ['kanban_jobs'] },
  { ui: 'Pipeline', uiMethod: 'loadHistory()', action: 'View history', controller: 'KanbanController', endpoint: '/kanban/{id}/history', method: 'GET', sql: 'SELECT', tables: ['kanban_history'] },
  { ui: 'Pipeline', uiMethod: 'loadNotes()', action: 'Load notes', controller: 'KanbanController', endpoint: '/kanban/{id}/notes', method: 'GET', sql: 'SELECT', tables: ['kanban_notes'] },
  { ui: 'Pipeline', uiMethod: 'saveNote()', action: 'Add note', controller: 'KanbanController', endpoint: '/kanban/{id}/notes', method: 'POST', sql: 'INSERT', tables: ['kanban_notes'] },
  { ui: 'Pipeline', uiMethod: 'deleteNote()', action: 'Delete note', controller: 'KanbanController', endpoint: '/kanban/notes/{id}', method: 'DELETE', sql: 'DELETE', tables: ['kanban_notes'] },
  { ui: 'Pipeline', uiMethod: 'startTimer()/stopTimer()', action: 'Save timer', controller: 'KanbanController', endpoint: '/kanban/{id}/timer', method: 'POST', sql: 'INSERT', tables: ['job_timers'] },
  { ui: 'Pipeline', uiMethod: 'loadTimers()', action: 'Load timers', controller: 'KanbanController', endpoint: '/kanban/{id}/timers', method: 'GET', sql: 'SELECT', tables: ['job_timers'] },
  { ui: 'Pipeline', uiMethod: 'deleteCard()', action: 'Delete card', controller: 'JobsController', endpoint: '/jobs/{id}', method: 'DELETE', sql: 'DELETE', tables: ['job_listings', 'kanban_jobs'] },
  { ui: 'Add Job', uiMethod: 'handleSubmit()', action: 'Create manual job entry', controller: 'JobsController', endpoint: '/jobs/manual', method: 'POST', sql: 'INSERT', tables: ['job_listings', 'searches', 'kanban_jobs'] },
  { ui: 'Import Links', uiMethod: 'fetchLinks()', action: 'Load link queue', controller: 'LinksController', endpoint: '/links', method: 'GET', sql: 'SELECT', tables: ['job_links'] },
  { ui: 'Import Links', uiMethod: 'addLinks()', action: 'Add link to queue', controller: 'LinksController', endpoint: '/links', method: 'POST', sql: 'INSERT', tables: ['job_links'] },
  { ui: 'Import Links', uiMethod: 'deleteLink()', action: 'Delete a link', controller: 'LinksController', endpoint: '/links/{id}', method: 'DELETE', sql: 'DELETE', tables: ['job_links'] },
  { ui: 'Import Links', uiMethod: 'processLinks()', action: 'Process/import links', controller: 'LinksController', endpoint: '/links/process', method: 'POST', sql: 'UPDATE/INSERT', tables: ['job_links', 'job_listings', 'searches', 'kanban_jobs'] },
  { ui: 'Import Links', uiMethod: 'resetLink()', action: 'Reset link to pending', controller: 'LinksController', endpoint: '/links/{id}/reset', method: 'POST', sql: 'UPDATE', tables: ['job_links'] },
  { ui: 'Import Links', uiMethod: 'resetAll()', action: 'Reset all to pending', controller: 'LinksController', endpoint: '/links/reset-all', method: 'POST', sql: 'UPDATE', tables: ['job_links'] },
  { ui: 'Import Links', uiMethod: 'clearImported()', action: 'Clear all imported', controller: 'LinksController', endpoint: '/links/all', method: 'DELETE', sql: 'DELETE', tables: ['job_links'] },
  { ui: 'Useful Links', uiMethod: 'fetchLinks()', action: 'Load links', controller: 'UsefulLinksController', endpoint: '/useful-links', method: 'GET', sql: 'SELECT', tables: ['useful_links'] },
  { ui: 'Useful Links', uiMethod: 'addLink()', action: 'Add link', controller: 'UsefulLinksController', endpoint: '/useful-links', method: 'POST', sql: 'INSERT', tables: ['useful_links'] },
  { ui: 'Useful Links', uiMethod: 'saveEdit()', action: 'Edit link', controller: 'UsefulLinksController', endpoint: '/useful-links/{id}', method: 'PATCH', sql: 'UPDATE', tables: ['useful_links'] },
  { ui: 'Useful Links', uiMethod: 'deleteLink()', action: 'Delete link', controller: 'UsefulLinksController', endpoint: '/useful-links/{id}', method: 'DELETE', sql: 'DELETE', tables: ['useful_links'] },
  { ui: 'Developer', uiMethod: 'PromptsLog()', action: 'Load prompts', controller: 'PromptsController', endpoint: '/prompts', method: 'GET', sql: 'SELECT', tables: ['prompts_log'] },
  { ui: 'Settings', uiMethod: 'Settings()', action: 'Load settings', controller: 'SettingsController', endpoint: '/settings', method: 'GET', sql: 'SELECT', tables: ['settings'] },
  { ui: 'Settings', uiMethod: 'save()', action: 'Save settings', controller: 'SettingsController', endpoint: '/settings', method: 'PATCH', sql: 'INSERT/UPDATE', tables: ['settings'] },
  { ui: 'Developer', uiMethod: 'CodeStats()', action: 'Load stats', controller: 'CodeStatsController', endpoint: '/code-stats', method: 'GET', sql: 'SELECT', tables: ['code_stats'] },
  { ui: 'Developer', uiMethod: 'CodeStats()', action: 'Load files', controller: 'CodeStatsController', endpoint: '/code-files', method: 'GET', sql: 'SELECT', tables: ['code_files'] },
  { ui: 'Developer', uiMethod: 'runScan()', action: 'Run scan', controller: 'CodeStatsController', endpoint: '/code-stats/scan', method: 'POST', sql: 'DELETE/INSERT', tables: ['code_stats', 'code_files'] },
]

const METHOD_COLORS = {
  GET: '#2e7d32', POST: '#1a73e8', PATCH: '#f57c00',
  DELETE: '#c62828', PUT: '#7b1fa2',
}

const SQL_COLORS = {
  SELECT: '#0288d1', INSERT: '#2e7d32', UPDATE: '#f57c00',
  DELETE: '#c62828', 'INSERT/UPDATE': '#7b1fa2',
  'SELECT/INSERT': '#546e7a', 'UPDATE/INSERT': '#ef6c00',
  'DELETE/INSERT': '#b71c1c',
}

const UI_PAGES = [...new Set(API_MAP.map(r => r.ui))]
const CONTROLLERS = [...new Set(API_MAP.map(r => r.controller))]

export default function CodeMap() {
  const [subTab, setSubTab] = useState('prompts')
  const [filterPage, setFilterPage] = useState('')
  const [filterMethod, setFilterMethod] = useState('')
  const [filterTable, setFilterTable] = useState('')
  const [filterController, setFilterController] = useState('')

  const allTables = [...new Set(API_MAP.flatMap(r => r.tables))].sort()

  const filtered = API_MAP.filter(r => {
    if (filterPage && r.ui !== filterPage) return false
    if (filterMethod && r.method !== filterMethod) return false
    if (filterTable && !r.tables.includes(filterTable)) return false
    if (filterController && r.controller !== filterController) return false
    return true
  })

  return (
    <div className="codemap-page">
      <h2>Code Map</h2>

      <div className="codemap-subtabs">
        <button className={subTab === 'prompts' ? 'codemap-subtab active' : 'codemap-subtab'} onClick={() => setSubTab('prompts')}>Prompts Log</button>
        <button className={subTab === 'codestats' ? 'codemap-subtab active' : 'codemap-subtab'} onClick={() => setSubTab('codestats')}>Code Stats</button>
        <button className={subTab === 'flow' ? 'codemap-subtab active' : 'codemap-subtab'} onClick={() => setSubTab('flow')}>Code Map</button>
        <button className={subTab === 'swagger' ? 'codemap-subtab active' : 'codemap-subtab'} onClick={() => setSubTab('swagger')}>API Documentation</button>
      </div>

      {subTab === 'prompts' && <PromptsLog />}
      {subTab === 'codestats' && <CodeStats />}
      {subTab === 'swagger' && <SwaggerDocs />}

      {subTab === 'flow' && (
        <>
      <p className="subtitle">Data flow: UI Action → Controller → API Endpoint → SQL Operation → Database Tables</p>

      <div className="codemap-filters">
        <select value={filterPage} onChange={e => setFilterPage(e.target.value)}>
          <option value="">All Pages</option>
          {UI_PAGES.map(p => <option key={p} value={p}>{p}</option>)}
        </select>
        <select value={filterController} onChange={e => setFilterController(e.target.value)}>
          <option value="">All Controllers</option>
          {CONTROLLERS.map(c => <option key={c} value={c}>{c}</option>)}
        </select>
        <select value={filterMethod} onChange={e => setFilterMethod(e.target.value)}>
          <option value="">All Methods</option>
          {['GET','POST','PATCH','DELETE'].map(m => <option key={m} value={m}>{m}</option>)}
        </select>
        <select value={filterTable} onChange={e => setFilterTable(e.target.value)}>
          <option value="">All Tables</option>
          {allTables.map(t => <option key={t} value={t}>{t}</option>)}
        </select>
      </div>

      <div className="codemap-flow-header">
        <span className="codemap-col-header">UI Page</span>
        <span className="codemap-col-header">UI Method</span>
        <span className="codemap-col-header">Controller</span>
        <span className="codemap-col-header">API Endpoint</span>
        <span className="codemap-col-header">Method</span>
        <span className="codemap-col-header">SQL</span>
        <span className="codemap-col-header">Tables</span>
      </div>

      <div className="codemap-flow-body">
        {filtered.map((r, i) => (
          <div key={i} className="codemap-row">
            <span className="codemap-cell codemap-ui">{r.ui}</span>
            <span className="codemap-cell codemap-uimethod"><code>{r.uiMethod}</code></span>
            <span className="codemap-cell codemap-controller">{r.controller}</span>
            <span className="codemap-cell codemap-endpoint"><code>{r.endpoint}</code></span>
            <span className="codemap-cell codemap-method" style={{ color: METHOD_COLORS[r.method] }}>{r.method}</span>
            <span className="codemap-cell codemap-sql" style={{ color: SQL_COLORS[r.sql] || '#555' }}>{r.sql}</span>
            <span className="codemap-cell codemap-tables">
              {r.tables.map(t => <span key={t} className="codemap-table-pill">{t}</span>)}
            </span>
          </div>
        ))}
      </div>

      <div className="codemap-summary">
        <p>{filtered.length} endpoint(s) shown — {API_MAP.length} total</p>
      </div>
        </>
      )}
    </div>
  )
}
