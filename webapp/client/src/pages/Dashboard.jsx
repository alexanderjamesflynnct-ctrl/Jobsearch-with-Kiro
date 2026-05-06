import { useState, useEffect } from 'react'
import { anonymize as anonFn } from '../utils/anonymize'

const API = 'http://localhost:8000'

const PIE_COLORS = [
  '#1a73e8', '#2e7d32', '#f57c00', '#c62828', '#7b1fa2',
  '#0288d1', '#78909c', '#ef6c00', '#546e7a', '#d81b60',
]

function PieChart({ data, kanban, anonymize }) {
  const total = data.reduce((sum, d) => sum + d.count, 0)
  const [drilldown, setDrilldown] = useState(null)

  if (total === 0) return null

  const radius = 70
  const circumference = 2 * Math.PI * radius
  let cumulative = 0

  const segments = data.map((d, i) => {
    const pct    = d.count / total
    const offset = cumulative
    cumulative  += pct
    return { ...d, pct, offset, color: PIE_COLORS[i % PIE_COLORS.length] }
  })

  const handleClick = (outcome) => {
    // Find jobs matching this outcome
    const jobs = kanban.filter(c => {
      if (c.status === 'Failed') {
        return (c.fail_type || 'Unspecified') === outcome
      }
      return c.status === outcome
    })
    setDrilldown({ outcome, jobs })
  }

  return (
    <div className="pie-chart-container">
      <div className="pie-chart-svg-wrap">
        <svg viewBox="0 0 200 200" className="pie-chart-svg">
          {segments.map((s, i) => (
            <circle
              key={i}
              cx="100" cy="100" r={radius}
              fill="none"
              stroke={s.color}
              strokeWidth="30"
              strokeDasharray={`${s.pct * circumference} ${circumference}`}
              strokeDashoffset={-s.offset * circumference}
              transform="rotate(-90 100 100)"
              className="pie-segment"
              onClick={() => handleClick(s.outcome)}
            />
          ))}
          <text x="100" y="95" textAnchor="middle" className="pie-center-value">{total}</text>
          <text x="100" y="115" textAnchor="middle" className="pie-center-label">Total</text>
        </svg>
      </div>
      <div className="pie-legend">
        {segments.map((s, i) => (
          <div key={i} className="pie-legend-item pie-legend-clickable" onClick={() => handleClick(s.outcome)}>
            <span className="pie-legend-swatch" style={{ background: s.color }} />
            <span className="pie-legend-label">{s.outcome}</span>
            <span className="pie-legend-value">{s.count} ({(s.pct * 100).toFixed(0)}%)</span>
          </div>
        ))}
      </div>

      {/* Drilldown modal */}
      {drilldown && (
        <div className="pie-drilldown-overlay" onClick={() => setDrilldown(null)}>
          <div className="pie-drilldown-modal" onClick={e => e.stopPropagation()}>
            <div className="pie-drilldown-header">
              <h3>{drilldown.outcome}</h3>
              <span className="pie-drilldown-count">{drilldown.jobs.length} job(s)</span>
            </div>
            <div className="pie-drilldown-body">
              <table className="pie-drilldown-table">
                <thead>
                  <tr><th>Title</th><th>Company</th><th>Location</th><th>Source</th></tr>
                </thead>
                <tbody>
                  {drilldown.jobs.map(j => (
                    <tr key={j.id}>
                      <td>
                        {j.url
                          ? <a href={j.url} target="_blank" rel="noreferrer">{j.title}</a>
                          : j.title}
                      </td>
                      <td>{anonymize ? anonFn(j.company) : j.company}</td>
                      <td>{j.location}</td>
                      <td><span className={`badge badge-${j.source}`}>{j.source}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export default function Dashboard({ anonymize }) {
  const [stats, setStats]         = useState(null)
  const [kanban, setKanban]       = useState([])
  const [appliedCount, setAppliedCount] = useState(0)
  const [outcomes, setOutcomes]   = useState([])
  const [appliedToday, setAppliedToday] = useState(0)
  const [loading, setLoading]     = useState(true)

  useEffect(() => {
    Promise.all([
      fetch(`${API}/stats`).then(r => r.json()),
      fetch(`${API}/kanban`).then(r => r.json()),
      fetch(`${API}/stats/applied-count`).then(r => r.json()),
      fetch(`${API}/stats/outcomes`).then(r => r.json()),
      fetch(`${API}/stats/applied-today`).then(r => r.json()),
    ]).then(([s, k, a, o, t]) => {
      setStats(s)
      setKanban(k)
      setAppliedCount(a.count)
      setOutcomes(o)
      setAppliedToday(t.count)
    }).finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="loading">Loading dashboard...</div>

  // Pipeline breakdown
  const pipeline = {}
  kanban.forEach(c => { pipeline[c.status] = (pipeline[c.status] || 0) + 1 })

  // Recent activity — last 10 jobs added
  const recent = [...kanban]
    .sort((a, b) => (b.updated_at || '').localeCompare(a.updated_at || ''))
    .slice(0, 10)

  // Active focus job
  const activeJob = kanban.find(c => c.is_active)

  return (
    <div className="dashboard-page">
      <h2>Dashboard</h2>

      {/* Stats cards */}
      <div className="dash-cards">
        <div className="dash-card">
          <div className="dash-card-value">{stats?.total_jobs || 0}</div>
          <div className="dash-card-label">Total Jobs</div>
        </div>
        <div className="dash-card">
          <div className="dash-card-value">{appliedCount}</div>
          <div className="dash-card-label">Applied</div>
        </div>
        <div className="dash-card">
          <div className="dash-card-value">{pipeline['Interviewed'] || 0}</div>
          <div className="dash-card-label">Interviewed</div>
        </div>
        <div className="dash-card">
          <div className="dash-card-value">{pipeline['Researching'] || 0}</div>
          <div className="dash-card-label">Researching</div>
        </div>
        <div className="dash-card">
          <div className="dash-card-value">{pipeline['Failed'] || 0}</div>
          <div className="dash-card-label">Failed</div>
        </div>
      </div>

      {/* Active focus */}
      {activeJob && (
        <div className="dash-section">
          <h3>Current Focus</h3>
          <div className="dash-focus-card">
            <div className="dash-focus-title">{activeJob.title}</div>
            <div className="dash-focus-company">{anonymize ? anonFn(activeJob.company) : activeJob.company}</div>
            <div className="dash-focus-meta">
              {activeJob.location && <span>{activeJob.location}</span>}
              {activeJob.salary && <span className="dash-focus-salary">{activeJob.salary}</span>}
              {activeJob.is_remote && <span className="badge-remote badge-remote-remote">{activeJob.is_remote}</span>}
            </div>
            {activeJob.url && (
              <a href={activeJob.url} target="_blank" rel="noreferrer" className="kanban-tile-link">View Job →</a>
            )}
          </div>
        </div>
      )}

      {/* Pipeline breakdown */}
      <div className="dash-section">
        <h3>Pipeline Breakdown</h3>
        <div className="dash-pipeline">
          {Object.entries(pipeline).sort((a, b) => b[1] - a[1]).map(([status, count]) => (
            <div key={status} className="dash-pipeline-row">
              <span className="dash-pipeline-label">{status}</span>
              <div className="dash-pipeline-bar-bg">
                <div
                  className="dash-pipeline-bar"
                  style={{ width: `${Math.min(100, (count / kanban.length) * 100)}%` }}
                />
              </div>
              <span className="dash-pipeline-count">{count}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Application outcomes pie chart + today widget */}
      {outcomes.length > 0 && (
        <div className="dash-section">
          <h3>Application Outcomes</h3>
          <div className="dash-outcomes-row">
            <PieChart data={outcomes} kanban={kanban} anonymize={anonymize} />
            <div className="dash-today-widget">
              <div className={`dash-today-face ${appliedToday > 0 ? 'happy' : 'sad'}`}>
                {appliedToday > 0 ? '😊' : '😞'}
              </div>
              <div className="dash-today-text">
                {appliedToday > 0
                  ? <><strong>{appliedToday}</strong> application{appliedToday > 1 ? 's' : ''} today!</>
                  : <>No applications today</>
                }
              </div>
            </div>
          </div>
        </div>
      )}

      {/* By country */}
      {stats?.by_country?.length > 0 && (
        <div className="dash-section">
          <h3>Jobs by Country</h3>
          <div className="dash-countries">
            {stats.by_country.map(c => (
              <div key={c.country} className="dash-country-pill">
                <span className="dash-country-name">{c.country}</span>
                <span className="dash-country-count">{c.n}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Recent activity */}
      <div className="dash-section">
        <h3>Recent Activity</h3>
        <table className="dash-recent-table">
          <thead>
            <tr><th>Title</th><th>Company</th><th>Status</th><th>Updated</th></tr>
          </thead>
          <tbody>
            {recent.map(r => (
              <tr key={r.id}>
                <td className="title-cell">{r.title}</td>
                <td>{anonymize ? anonFn(r.company) : r.company}</td>
                <td><span className="badge">{r.status}</span></td>
                <td className="date">{r.updated_at?.replace('T',' ').replace('Z','')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
