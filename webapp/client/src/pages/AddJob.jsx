import { useState } from 'react'

const API = 'http://localhost:8000'

const COUNTRIES = ['USA', 'UK', 'Canada', 'Australia', 'Ireland', 'New Zealand', 'Singapore', 'India']
const REMOTE_OPTIONS = ['', 'Remote', 'Hybrid', 'On-site']
const SOURCES = ['manual', 'linkedin', 'indeed', 'glassdoor', 'ziprecruiter', 'referral', 'other']

const EMPTY = {
  title: '', company: '', location: '', city: '', state: '',
  country: '', job_type: '', salary: '', url: '', source: 'manual',
  is_remote: '', description: '',
}

export default function AddJob() {
  const [form, setForm]     = useState({ ...EMPTY })
  const [status, setStatus] = useState('')
  const [error, setError]   = useState('')
  const [saving, setSaving] = useState(false)

  const set = (key, value) => setForm(f => ({ ...f, [key]: value }))

  const handleSubmit = async (e) => {
    e.preventDefault()
    setStatus(''); setError('')
    setSaving(true)

    try {
      const res = await fetch(`${API}/jobs/manual`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(form),
      })
      const data = await res.json()
      if (!res.ok) {
        setError(data.error || 'Failed to save.')
      } else {
        setStatus(`Job added successfully (ID: ${data.id})`)
        setForm({ ...EMPTY })
      }
    } catch (err) {
      setError('Could not reach API: ' + err.message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="add-job-page">
      <h2>Add Opportunity</h2>
      <p className="subtitle">Manually enter a job opportunity. All fields are optional — fill in what you know.</p>

      {status && <div className="msg success">{status}</div>}
      {error  && <div className="msg error">{error}</div>}

      <form className="add-job-form" onSubmit={handleSubmit}>
        <div className="form-row">
          <label>
            <span>Job Title</span>
            <input type="text" value={form.title} onChange={e => set('title', e.target.value)} placeholder="e.g. Director of Engineering" />
          </label>
          <label>
            <span>Company</span>
            <input type="text" value={form.company} onChange={e => set('company', e.target.value)} placeholder="e.g. Acme Corp" />
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Location</span>
            <input type="text" value={form.location} onChange={e => set('location', e.target.value)} placeholder="e.g. New York, NY" />
          </label>
          <label>
            <span>City</span>
            <input type="text" value={form.city} onChange={e => set('city', e.target.value)} placeholder="e.g. New York" />
          </label>
          <label>
            <span>State</span>
            <input type="text" value={form.state} onChange={e => set('state', e.target.value)} placeholder="e.g. NY" maxLength={2} />
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Country</span>
            <select value={form.country} onChange={e => set('country', e.target.value)}>
              <option value="">Select...</option>
              {COUNTRIES.map(c => <option key={c} value={c}>{c}</option>)}
            </select>
          </label>
          <label>
            <span>Work Mode</span>
            <select value={form.is_remote} onChange={e => set('is_remote', e.target.value)}>
              {REMOTE_OPTIONS.map(r => <option key={r} value={r}>{r || 'Not specified'}</option>)}
            </select>
          </label>
          <label>
            <span>Job Type</span>
            <input type="text" value={form.job_type} onChange={e => set('job_type', e.target.value)} placeholder="e.g. Full-time" />
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Salary</span>
            <input type="text" value={form.salary} onChange={e => set('salary', e.target.value)} placeholder="e.g. $150,000 - $200,000" />
          </label>
          <label>
            <span>Source</span>
            <select value={form.source} onChange={e => set('source', e.target.value)}>
              {SOURCES.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          </label>
        </div>

        <div className="form-row">
          <label className="full-width">
            <span>Job URL</span>
            <input type="text" value={form.url} onChange={e => set('url', e.target.value)} placeholder="https://..." />
          </label>
        </div>

        <div className="form-row">
          <label className="full-width">
            <span>Description / Notes</span>
            <textarea value={form.description} onChange={e => set('description', e.target.value)} rows={4} placeholder="Paste job description or add notes..." />
          </label>
        </div>

        <div className="form-actions">
          <button type="submit" className="btn-run" disabled={saving}>
            {saving ? 'Saving...' : '+ Add Opportunity'}
          </button>
          <button type="button" className="btn-clear" onClick={() => setForm({ ...EMPTY })}>
            Clear Form
          </button>
        </div>
      </form>
    </div>
  )
}
