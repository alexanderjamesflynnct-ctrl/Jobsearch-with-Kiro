import { useState, useEffect } from 'react'

const API = 'http://localhost:8000'

const COLORS = [
  '#1a73e8', '#2e7d32', '#f57c00', '#c62828', '#7b1fa2',
  '#0288d1', '#78909c', '#ef6c00', '#546e7a', '#d81b60',
  '#00838f', '#6d4c41', '#37474f', '#ff6d00', '#4527a0',
]

function PieChart({ data, valueKey, labelKey, title }) {
  const total = data.reduce((sum, d) => sum + d[valueKey], 0)
  if (total === 0) return null

  const radius = 70
  const circumference = 2 * Math.PI * radius
  let cumulative = 0

  const segments = data.map((d, i) => {
    const pct    = d[valueKey] / total
    const offset = cumulative
    cumulative  += pct
    return { ...d, pct, offset, color: COLORS[i % COLORS.length] }
  })

  return (
    <div className="code-stats-chart">
      <h3>{title}</h3>
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
              />
            ))}
            <text x="100" y="95" textAnchor="middle" className="pie-center-value">{total.toLocaleString()}</text>
            <text x="100" y="115" textAnchor="middle" className="pie-center-label">{valueKey === 'line_count' ? 'Lines' : 'Files'}</text>
          </svg>
        </div>
        <div className="pie-legend">
          {segments.map((s, i) => (
            <div key={i} className="pie-legend-item">
              <span className="pie-legend-swatch" style={{ background: s.color }} />
              <span className="pie-legend-label">{s[labelKey]}</span>
              <span className="pie-legend-value">{s[valueKey].toLocaleString()} ({(s.pct * 100).toFixed(1)}%)</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

export default function CodeStats() {
  const [data, setData]         = useState(null)
  const [loading, setLoading]   = useState(true)
  const [scanning, setScanning] = useState(false)
  const [scanMsg, setScanMsg]   = useState('')

  const fetchStats = () => {
    fetch(`${API}/code-stats`)
      .then(r => r.json())
      .then(setData)
      .finally(() => setLoading(false))
  }

  useEffect(() => { fetchStats() }, [])

  const runScan = async () => {
    setScanning(true)
    setScanMsg('')
    try {
      const res  = await fetch(`${API}/code-stats/scan`, { method: 'POST' })
      const result = await res.json()
      if (result.success) {
        setScanMsg('Scan complete.')
        fetchStats()
      } else {
        setScanMsg('Scan failed: ' + (result.errors || result.output))
      }
    } catch (e) {
      setScanMsg('Error: ' + e.message)
    } finally {
      setScanning(false)
    }
  }

  if (loading) return <div className="loading">Loading code stats...</div>

  if (!data || !data.breakdown?.length) return (
    <div className="code-stats-page">
      <div className="header-row">
        <h2>Source Code Statistics</h2>
        <button className="btn-run-adzuna" onClick={runScan} disabled={scanning}>
          {scanning ? '⟳ Scanning...' : '⟳ Rescan'}
        </button>
      </div>
      {scanMsg && <div className="msg success">{scanMsg}</div>}
      <p className="subtitle">No stats available. Click Rescan to generate.</p>
    </div>
  )

  const scannedAt = data.breakdown[0]?.scanned_at?.replace('T', ' ').replace('Z', ' UTC')

  return (
    <div className="code-stats-page">
      <div className="header-row">
        <h2>Source Code Statistics</h2>
        <button className="btn-run-adzuna" onClick={runScan} disabled={scanning}>
          {scanning ? '⟳ Scanning...' : '⟳ Rescan'}
        </button>
      </div>
      {scanMsg && <div className="msg success">{scanMsg}</div>}
      <p className="subtitle">
        {data.total_files} files, {data.total_lines.toLocaleString()} total lines of code.
        {scannedAt && <span> Last scanned: {scannedAt}</span>}
      </p>

      <div className="code-stats-cards">
        <div className="dash-card">
          <div className="dash-card-value">{data.total_files}</div>
          <div className="dash-card-label">Files</div>
        </div>
        <div className="dash-card">
          <div className="dash-card-value">{data.total_lines.toLocaleString()}</div>
          <div className="dash-card-label">Lines of Code</div>
        </div>
        <div className="dash-card">
          <div className="dash-card-value">{data.breakdown.length}</div>
          <div className="dash-card-label">Languages</div>
        </div>
      </div>

      <div className="code-stats-charts">
        <PieChart data={data.breakdown} valueKey="line_count" labelKey="file_type" title="Lines by Language" />
        <PieChart data={data.breakdown} valueKey="file_count" labelKey="file_type" title="Files by Language" />
      </div>

      <div className="code-stats-table-section">
        <h3>Breakdown</h3>
        <table className="dash-recent-table">
          <thead>
            <tr><th>Language / Type</th><th>Files</th><th>Lines</th><th>% of Total</th></tr>
          </thead>
          <tbody>
            {data.breakdown.map((r, i) => (
              <tr key={i}>
                <td className="title-cell">{r.file_type}</td>
                <td>{r.file_count}</td>
                <td>{r.line_count.toLocaleString()}</td>
                <td>{((r.line_count / data.total_lines) * 100).toFixed(1)}%</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
