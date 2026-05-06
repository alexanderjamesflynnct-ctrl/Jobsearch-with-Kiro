import { useState, useEffect } from 'react'

const API = 'http://localhost:8000'

const CATEGORY_COLORS = {
  'Feature':        '#1a73e8',
  'Bug Fix':        '#c62828',
  'UI Improvement': '#7b1fa2',
  'Refactor':       '#f57c00',
  'Environment':    '#0288d1',
  'Maintenance':    '#78909c',
  'Research':       '#2e7d32',
  'Configuration':  '#546e7a',
  'Organization':   '#6d4c41',
  'Usage':          '#00838f',
  'Rename':         '#ef6c00',
  'Documentation':  '#455a64',
  'Process':        '#37474f',
}

export default function PromptsLog() {
  const [prompts, setPrompts]   = useState([])
  const [filter, setFilter]     = useState('')
  const [selected, setSelected] = useState(null)

  useEffect(() => {
    fetch(`${API}/prompts`).then(r => r.json()).then(setPrompts)
  }, [])

  const filtered = filter
    ? prompts.filter(p =>
        p.prompt?.toLowerCase().includes(filter.toLowerCase()) ||
        p.category?.toLowerCase().includes(filter.toLowerCase()) ||
        p.response?.toLowerCase().includes(filter.toLowerCase())
      )
    : prompts

  return (
    <div className="prompts-page">
      <h2>Prompts Log</h2>
      <p className="subtitle">{prompts.length} prompts used to build this application. Click a row to see the full response.</p>

      <input
        className="prompts-search"
        type="text"
        placeholder="Filter by prompt, category, or response..."
        value={filter}
        onChange={e => setFilter(e.target.value)}
      />

      <div className="prompts-list">
        <table className="prompts-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Date</th>
              <th>Prompt</th>
              <th>Category</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map(p => (
              <tr
                key={p.sequence}
                className={`prompts-row ${p.response ? 'has-response' : ''}`}
                onClick={() => setSelected(p)}
              >
                <td className="prompts-seq">{p.sequence}</td>
                <td className="prompts-date">{p.date}</td>
                <td className="prompts-text">{p.prompt}</td>
                <td>
                  <span
                    className="prompts-category"
                    style={{ background: CATEGORY_COLORS[p.category] || '#888' }}
                  >
                    {p.category}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Detail modal */}
      {selected && (
        <div className="prompts-modal-overlay" onClick={() => setSelected(null)}>
          <div className="prompts-modal" onClick={e => e.stopPropagation()}>
            <div className="prompts-modal-header">
              <span className="prompts-modal-seq">#{selected.sequence}</span>
              <span
                className="prompts-category"
                style={{ background: CATEGORY_COLORS[selected.category] || '#888' }}
              >
                {selected.category}
              </span>
              <span className="prompts-modal-date">{selected.date}</span>
            </div>
            <div className="prompts-modal-prompt">
              <label>Prompt</label>
              <p>{selected.prompt}</p>
            </div>
            <div className="prompts-modal-response">
              <label>Response</label>
              <div className="prompts-modal-response-text">
                {selected.response || 'No response recorded.'}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
