import { useState, useRef, useEffect } from 'react'

const REMOTE_OPTIONS = ['Remote', 'Hybrid', 'On-site']

export default function Filters({ filters, countries, sources, jobTypes, states, onChange }) {
  const [local, setLocal]       = useState(filters)
  const [typeOpen, setTypeOpen] = useState(false)
  const typeRef                 = useRef(null)

  // Close dropdown when clicking outside
  useEffect(() => {
    const handler = (e) => {
      if (typeRef.current && !typeRef.current.contains(e.target)) setTypeOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const set = (key, value) => {
    const updated = { ...local, [key]: value }
    setLocal(updated)
    if (key !== 'keywords') onChange(updated)
  }

  const handleSearch = (e) => {
    e.preventDefault()
    onChange(local)
  }

  const handleClear = () => {
    const cleared = {
      keywords: '', country: '', source: '', job_type: '',
      is_remote: '', state: '', sort: 'searched_at', order: 'desc', per_page: 25,
    }
    setLocal(cleared)
    onChange(cleared)
  }

  const hasFilters = local.keywords || local.country || local.source ||
                     local.job_type || local.is_remote || local.state

  return (
    <div className="filters-panel">
      {/* Search bar */}
      <form className="filters-row" onSubmit={handleSearch}>
        <input
          type="text"
          placeholder="Search title, company, description..."
          value={local.keywords}
          onChange={e => setLocal({ ...local, keywords: e.target.value })}
        />
        <button type="submit" className="btn-search">Search</button>
        {hasFilters && (
          <button type="button" className="btn-clear" onClick={handleClear}>✕ Clear</button>
        )}
      </form>

      {/* Filter dropdowns */}
      <div className="filters-row filters-dropdowns">

        <label>
          <span>Source</span>
          <select value={local.source} onChange={e => set('source', e.target.value)}>
            <option value="">All Sources</option>
            {sources.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </label>

        <label>
          <span>Country</span>
          <select value={local.country} onChange={e => set('country', e.target.value)}>
            <option value="">All Countries</option>
            {countries.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
        </label>

        <label>
          <span>State</span>
          <select value={local.state} onChange={e => set('state', e.target.value)}>
            <option value="">All States</option>
            {states.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </label>

        {/* Job Type + Remote combined — collapsible dropdown */}
        <div className="filter-group" ref={typeRef}>
          <span className="filter-group-label">Job Type / Work Mode</span>
          <button
            type="button"
            className="type-dropdown-btn"
            onClick={() => setTypeOpen(o => !o)}
          >
            {[local.job_type, local.is_remote].filter(Boolean).join(' · ') || 'All'}
            <span className="type-dropdown-arrow">{typeOpen ? '▲' : '▼'}</span>
          </button>

          {typeOpen && (
            <div className="multiselect-box">
              <div className="multiselect-section-label">Type</div>
              {jobTypes.map(t => (
                <label key={t} className="multiselect-option">
                  <input
                    type="radio"
                    name="job_type"
                    value={t}
                    checked={local.job_type === t}
                    onChange={() => set('job_type', local.job_type === t ? '' : t)}
                  />
                  {t}
                </label>
              ))}
              <label className="multiselect-option">
                <input
                  type="radio"
                  name="job_type"
                  value=""
                  checked={local.job_type === ''}
                  onChange={() => set('job_type', '')}
                />
                All Types
              </label>

              <div className="multiselect-divider" />
              <div className="multiselect-section-label">Work Mode</div>
              {REMOTE_OPTIONS.map(r => (
                <label key={r} className="multiselect-option">
                  <input
                    type="radio"
                    name="is_remote"
                    value={r}
                    checked={local.is_remote === r}
                    onChange={() => set('is_remote', local.is_remote === r ? '' : r)}
                  />
                  {r}
                </label>
              ))}
              <label className="multiselect-option">
                <input
                  type="radio"
                  name="is_remote"
                  value=""
                  checked={local.is_remote === ''}
                  onChange={() => set('is_remote', '')}
                />
                All Modes
              </label>
            </div>
          )}
        </div>

        <label>
          <span>Sort By</span>
          <select value={local.sort} onChange={e => set('sort', e.target.value)}>
            <option value="searched_at">Search Date</option>
            <option value="date_posted">Posted Date</option>
            <option value="title">Title</option>
            <option value="company">Company</option>
            <option value="country">Country</option>
            <option value="source">Source</option>
            <option value="salary">Salary</option>
            <option value="state">State</option>
          </select>
        </label>

        <label>
          <span>Order</span>
          <select value={local.order} onChange={e => set('order', e.target.value)}>
            <option value="desc">Newest First</option>
            <option value="asc">Oldest First</option>
          </select>
        </label>

        <label>
          <span>Per Page</span>
          <select value={local.per_page} onChange={e => set('per_page', Number(e.target.value))}>
            <option value={25}>25</option>
            <option value={50}>50</option>
            <option value={100}>100</option>
          </select>
        </label>

      </div>
    </div>
  )
}
