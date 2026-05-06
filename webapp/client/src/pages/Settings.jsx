import { useState, useEffect } from 'react'

const API = 'http://localhost:8000'

const TIMEZONES = Intl.supportedValuesOf('timeZone')

export default function Settings() {
  const [settings, setSettings] = useState({})
  const [saving, setSaving]     = useState(false)
  const [status, setStatus]     = useState('')

  useEffect(() => {
    fetch(`${API}/settings`).then(r => r.json()).then(setSettings)
  }, [])

  const selected = settings.timezone || 'America/New_York'
  const keywords = settings.search_keywords || ''

  const save = async (key, value) => {
    setSaving(true)
    setStatus('')
    await fetch(`${API}/settings`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ [key]: value }),
    })
    setSettings(s => ({ ...s, [key]: value }))
    setSaving(false)
    setStatus(`Saved: ${key} = ${value}`)
  }

  return (
    <div className="settings-page">
      <h2>Settings</h2>

      <div className="settings-section">
        <h3>Search Keywords</h3>
        <p className="subtitle">
          Default job title used when fetching Adzuna jobs.
        </p>
        <input
          className="settings-input"
          type="text"
          value={keywords}
          onChange={e => setSettings(s => ({ ...s, search_keywords: e.target.value }))}
          onBlur={e => save('search_keywords', e.target.value)}
          disabled={saving}
          placeholder="e.g. Director of Software Engineering"
        />
      </div>

      <div className="settings-section">
        <h3>Timezone</h3>
        <p className="subtitle">
          Used for "applied today" calculations and date displays.
        </p>

        <select
          className="settings-tz-select"
          value={selected}
          onChange={e => save('timezone', e.target.value)}
          disabled={saving}
        >
          {TIMEZONES.map(tz => (
            <option key={tz} value={tz}>{tz}</option>
          ))}
        </select>
      </div>

      {status && <div className="msg success" style={{marginTop:'10px'}}>{status}</div>}
    </div>
  )
}
