import { useState, useEffect, useCallback } from 'react'
import JobTable from '../components/JobTable'
import Filters from '../components/Filters'
import StatsBar from '../components/StatsBar'

const API = 'http://localhost:8000'

export default function JobSearch({ anonymize, setAnonymize }) {
  const [jobs, setJobs]           = useState([])
  const [stats, setStats]         = useState(null)
  const [countries, setCountries] = useState([])
  const [sources, setSources]     = useState([])
  const [jobTypes, setJobTypes]   = useState([])
  const [states, setStates]       = useState([])
  const [total, setTotal]         = useState(0)
  const [page, setPage]           = useState(1)
  const [loading, setLoading]     = useState(false)
  const [error, setError]         = useState(null)
  const [running, setRunning]     = useState(false)
  const [runResult, setRunResult] = useState(null)
  const [showRun, setShowRun]     = useState(false)
  const [runKeywords, setRunKeywords] = useState('')

  // Load search_keywords setting
  useEffect(() => {
    fetch(`${API}/settings`).then(r => r.json()).then(s => {
      setRunKeywords(s.search_keywords || 'Director of Software Engineering')
    }).catch(() => {})
  }, [])

  const [filters, setFilters] = useState({
    keywords: '',
    country: '',
    source: '',
    job_type: '',
    is_remote: '',
    state: '',
    sort: 'searched_at',
    order: 'desc',
    per_page: 25,
  })

  const fetchJobs = useCallback(async (f, p) => {
    setLoading(true)
    setError(null)
    try {
      const params = new URLSearchParams({ ...f, page: p })
      const res = await fetch(`${API}/jobs?${params}`)
      if (!res.ok) throw new Error(`API error ${res.status}`)
      const data = await res.json()
      setJobs(data.jobs)
      setTotal(data.total)
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetch(`${API}/stats`).then(r => r.json()).then(setStats).catch(() => {})
    fetch(`${API}/countries`).then(r => r.json()).then(setCountries).catch(() => {})
    fetch(`${API}/sources`).then(r => r.json()).then(setSources).catch(() => {})
    fetch(`${API}/job-types`).then(r => r.json()).then(setJobTypes).catch(() => {})
    fetch(`${API}/states`).then(r => r.json()).then(setStates).catch(() => {})
  }, [])

  useEffect(() => { fetchJobs(filters, page) }, [filters, page, fetchJobs])

  const handleFilterChange = (f) => { setFilters(f); setPage(1) }
  const totalPages = Math.ceil(total / filters.per_page)

  const deleteJob = async (id) => {
    await fetch(`${API}/jobs/${encodeURIComponent(id)}`, { method: 'DELETE' })
    fetchJobs(filters, page)
  }

  const runAdzuna = async () => {
    setRunning(true)
    setRunResult(null)
    try {
      const res  = await fetch(`${API}/run-adzuna`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ keywords: runKeywords }),
      })
      const data = await res.json()
      setRunResult(data)
      if (data.success) {
        // Refresh stats and jobs after successful run
        fetch(`${API}/stats`).then(r => r.json()).then(setStats).catch(() => {})
        fetchJobs(filters, page)
      }
    } catch (e) {
      setRunResult({ success: false, errors: e.message })
    } finally {
      setRunning(false)
    }
  }

  return (
    <>
      <header className="app-header">
        <div className="header-row">
          <h1>Job Search Results</h1>
          <button className="btn-run-adzuna" onClick={() => { setShowRun(true); setRunResult(null) }}>
            ⟳ Fetch Adzuna Jobs
          </button>
        </div>
        {stats && <StatsBar stats={stats} />}
      </header>

      {/* Adzuna run modal */}
      {showRun && (
        <div className="run-modal-overlay" onClick={() => !running && setShowRun(false)}>
          <div className="run-modal" onClick={e => e.stopPropagation()}>
            <h3>Fetch Adzuna Jobs</h3>
            <p className="subtitle">Searches all countries in countries.json. Duplicates are automatically skipped.</p>
            <div className="run-modal-input">
              <label>Keywords</label>
              <input
                type="text"
                value={runKeywords}
                onChange={e => setRunKeywords(e.target.value)}
                disabled={running}
              />
            </div>
            <div className="run-modal-actions">
              <button className="btn-run" onClick={runAdzuna} disabled={running || !runKeywords.trim()}>
                {running ? '⟳ Running...' : '▶ Run Search'}
              </button>
              <button className="btn-cancel-sm" onClick={() => setShowRun(false)} disabled={running}>
                Cancel
              </button>
            </div>
            {running && <div className="run-progress">Searching across countries — this may take a minute...</div>}
            {runResult && (
              <div className={`run-output ${runResult.success ? 'run-success' : 'run-error'}`}>
                <pre>{runResult.output || runResult.errors}</pre>
              </div>
            )}
          </div>
        </div>
      )}

      <Filters filters={filters} countries={countries} sources={sources} jobTypes={jobTypes} states={states} onChange={handleFilterChange} />

      {error && (
        <div className="error-banner">
          Could not connect to API — make sure the backend is running:<br />
          <code>dotnet run</code> in <code>webapp/api_cs/</code>
        </div>
      )}

      {loading ? (
        <div className="loading">Loading...</div>
      ) : (
        <>
          <div className="results-count">{total} job{total !== 1 ? 's' : ''} found</div>
          <JobTable jobs={jobs} onDelete={deleteJob} onRefresh={() => fetchJobs(filters, page)} anonymize={anonymize} />
          {totalPages > 1 && (
            <div className="pagination">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}>← Prev</button>
              <span>Page {page} of {totalPages}</span>
              <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}>Next →</button>
            </div>
          )}
        </>
      )}
    </>
  )
}
