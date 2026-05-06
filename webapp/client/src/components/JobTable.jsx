import { useState, useEffect } from 'react'
import { getTimezoneLabel } from '../utils/timezone'
import { anonymize as anonFn } from '../utils/anonymize'

const API = 'http://localhost:8000'
const NEW_WINDOW_MS = 10 * 60 * 1000  // 10 minutes

function isNew(searchedAt) {
  if (!searchedAt) return false
  return (Date.now() - new Date(searchedAt).getTime()) < NEW_WINDOW_MS
}

const KNOWN_COUNTRIES = [
  'USA', 'UK', 'Canada', 'Australia', 'Ireland', 'New Zealand',
  'Singapore', 'South Africa', 'Nigeria', 'Philippines', 'India',
  'Pakistan', 'Hong Kong',
]

function CountryCell({ job, onUpdated }) {
  const [editing, setEditing]   = useState(false)
  const [selected, setSelected] = useState('')
  const [saving, setSaving]     = useState(false)

  const isUnknown = !job.country || job.country === 'Unknown' || job.country === 'LinkedIn'

  const save = async (e) => {
    e.stopPropagation()
    if (!selected) return
    setSaving(true)
    await fetch(`${API}/jobs/${encodeURIComponent(job.id)}/country`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ country: selected }),
    })
    setSaving(false)
    setEditing(false)
    onUpdated()
  }

  if (editing) {
    return (
      <div className="country-edit" onClick={e => e.stopPropagation()}>
        <select
          autoFocus
          value={selected}
          onChange={e => setSelected(e.target.value)}
        >
          <option value="">Select...</option>
          {KNOWN_COUNTRIES.map(c => <option key={c} value={c}>{c}</option>)}
        </select>
        <button className="btn-save-sm" onClick={save} disabled={!selected || saving}>
          {saving ? '...' : 'Save'}
        </button>
        <button className="btn-cancel-sm" onClick={e => { e.stopPropagation(); setEditing(false) }}>
          ✕
        </button>
      </div>
    )
  }

  return (
    <div className="country-cell">
      <span className={`badge ${isUnknown ? 'badge-unknown' : ''}`}>
        {job.country || 'Unknown'}
      </span>
      {isUnknown && (
        <button
          className="btn-edit-country"
          title="Set country"
          onClick={e => { e.stopPropagation(); setEditing(true) }}
        >
          ✎
        </button>
      )}
    </div>
  )
}

export default function JobTable({ jobs, onDelete, onRefresh, anonymize }) {
  const [expanded, setExpanded] = useState(null)
  const co = (name) => anonymize ? anonFn(name) : name

  const [, setTick] = useState(0)

  // Re-render every minute so "New" badges expire automatically
  useEffect(() => {
    const id = setInterval(() => setTick(t => t + 1), 60_000)
    return () => clearInterval(id)
  }, [])

  if (!jobs.length) {
    return <div className="no-results">No jobs found. Try adjusting your filters.</div>
  }

  return (
    <table className="job-table">
      <thead>
        <tr>
          <th>Source</th>
          <th>Title</th>
          <th>Company</th>
          <th>Location / Timezone</th>
          <th>State</th>
          <th>Country</th>
          <th>Type</th>
          <th>Salary</th>
          <th>Posted</th>
          <th>Found</th>
          <th>Link</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        {jobs.map(job => (
          <>
            <tr
              key={job.id}
              className={expanded === job.id ? 'expanded-row' : ''}
              onClick={() => setExpanded(expanded === job.id ? null : job.id)}
              style={{ cursor: 'pointer' }}
            >
              <td>
                <div style={{display:'flex', flexDirection:'column', gap:'3px', alignItems:'flex-start'}}>
                  <span className={`badge badge-${job.source || 'adzuna'}`}>{job.source || 'adzuna'}</span>
                  {isNew(job.searched_at) && <span className="badge-new">New</span>}
                </div>
              </td>
              <td className="title-cell">{job.title}</td>
              <td>{co(job.company)}</td>
              <td>
                <div>{job.location}</div>
                {getTimezoneLabel(job.country, job.state) &&
                  <div className="tz-label">{getTimezoneLabel(job.country, job.state)}</div>
                }
              </td>
              <td>{job.state || '—'}</td>
              <td><CountryCell job={job} onUpdated={onRefresh} /></td>
              <td>
                <div className="type-cell">
                  <span>{job.job_type || '—'}</span>
                  {job.is_remote && (
                    <span className={`badge-remote badge-remote-${job.is_remote.toLowerCase().replace('-','')}`}>
                      {job.is_remote}
                    </span>
                  )}
                </div>
              </td>
              <td className="salary">{job.salary || '—'}</td>
              <td className="date">{job.date_posted || '—'}</td>
              <td className="date tz-est">{job.searched_at?.slice(0, 10)}</td>
              <td onClick={e => e.stopPropagation()}>
                {job.url ? <a href={job.url} target="_blank" rel="noreferrer">View →</a> : '—'}
              </td>
              <td onClick={e => e.stopPropagation()}>
                <button className="btn-delete" onClick={() => onDelete(job.id)} title="Delete">✕</button>
              </td>
            </tr>
            {expanded === job.id && job.description && (
              <tr key={`${job.id}-desc`} className="description-row">
                <td colSpan={12}>
                  <div className="description">{job.description}</div>
                </td>
              </tr>
            )}
          </>
        ))}
      </tbody>
    </table>
  )
}
