import { useState, useEffect, useCallback, useRef } from 'react'
import { anonymize as anonFn } from '../utils/anonymize'

const API = 'http://localhost:8000'

const LANES = [
  'Searched/Found',
  'Researching',
  'Applied',
  'Interviewed',
  'Accepted Job',
  'Failed',
]

const LANE_COLORS = {
  'Searched/Found': '#1a73e8',
  'Researching':    '#7b1fa2',
  'Applied':        '#f57c00',
  'Interviewed':    '#0288d1',
  'Accepted Job':   '#2e7d32',
  'Failed':         '#c62828',
}

const FAIL_TYPES = [
  'Rejected By Me',
  'Not Heard Back (30d)',
  'Application Error',
  'Rejected by Employer',
  'No Longer Accepting Applications',
]

function Card({ card, onMove, onSave, onDragStart, anonymize }) {
  const [expanded, setExpanded]       = useState(false)
  const [newNote, setNewNote]         = useState('')
  const [saving, setSaving]           = useState(false)
  const [notes, setNotes]             = useState([])
  const [notesLoaded, setNotesLoaded] = useState(false)
  const [showHistory, setShowHistory] = useState(false)
  const [history, setHistory]         = useState([])
  const [loadingHist, setLoadingHist] = useState(false)

  const loadNotes = async () => {
    const res = await fetch(`${API}/kanban/${card.id}/notes`)
    setNotes(await res.json())
    setNotesLoaded(true)
  }

  const handleExpand = () => {
    const next = !expanded
    setExpanded(next)
    if (next && !notesLoaded) loadNotes()
  }

  const saveNote = async () => {
    if (!newNote.trim()) return
    setSaving(true)
    await fetch(`${API}/kanban/${card.id}/notes`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ note: newNote.trim() }),
    })
    setNewNote('')
    setSaving(false)
    setExpanded(false)
    loadNotes()
    onSave()
  }

  const deleteNote = async (noteId) => {
    await fetch(`${API}/kanban/notes/${noteId}`, { method: 'DELETE' })
    loadNotes()
  }

  const loadHistory = async (e) => {
    e.stopPropagation()
    setShowHistory(h => {
      if (!h) {
        setLoadingHist(true)
        fetch(`${API}/kanban/${card.id}/history`)
          .then(r => r.json())
          .then(data => { setHistory(data); setLoadingHist(false) })
      }
      return !h
    })
  }

  const co = (name) => anonymize ? anonFn(name) : name

  const setActive = async (e) => {
    e.stopPropagation()
    await fetch(`${API}/kanban/${card.id}/active`, { method: 'POST' })
    onSave()
  }

  const deleteCard = async (e) => {
    e.stopPropagation()
    if (!window.confirm(`Delete "${card.title}"?`)) return
    await fetch(`${API}/jobs/${encodeURIComponent(card.job_listing_id)}`, { method: 'DELETE' })
    onSave()
  }

  const setFailType = async (failType) => {
    await fetch(`${API}/kanban/${card.id}/fail-type`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fail_type: failType }),
    })
    onSave()
  }

  const currentIdx = LANES.indexOf(card.status)

  return (
    <div
      className={`kanban-card ${expanded ? 'kanban-card-expanded' : ''} ${card.is_active ? 'kanban-card-active' : ''}`}
      draggable
      onDragStart={e => {
        onDragStart(card.id)
        e.dataTransfer.effectAllowed = 'move'
        e.dataTransfer.setData('text/plain', String(card.id))
      }}
    >
      <div className="kanban-card-header" onClick={handleExpand}>
        <div className="kanban-card-title">{card.title}</div>
        <div className="kanban-card-company">{co(card.company)}</div>
        <div className="kanban-card-meta">
          {card.location && <span>{card.location}</span>}
          {card.salary   && <span className="kanban-salary">{card.salary}</span>}
        </div>
        <div className="kanban-card-badges">
          {card.source   && <span className={`badge badge-${card.source}`}>{card.source}</span>}
          {card.is_remote && <span className={`badge-remote badge-remote-${card.is_remote.toLowerCase().replace('-','')}`}>{card.is_remote}</span>}
          {card.fail_type && <span className="badge-fail-type">{card.fail_type}</span>}
          {card.url && (
            <a
              href={card.url}
              target="_blank"
              rel="noreferrer"
              className="kanban-tile-link"
              onClick={e => e.stopPropagation()}
            >
              View →
            </a>
          )}
          {card.status === 'Searched/Found' && (
            <button
              className={`kanban-active-btn ${card.is_active ? 'kanban-active-btn-on' : ''}`}
              onClick={setActive}
              title={card.is_active ? 'Deselect' : 'Mark as current focus'}
            >
              {card.is_active ? '★' : '☆'}
            </button>
          )}
          {card.status === 'Searched/Found' && (
            <button
              className="kanban-delete-btn"
              onClick={deleteCard}
              title="Delete this job"
            >
              ✕
            </button>
          )}
        </div>
      </div>

      {expanded && (
        <div className="kanban-card-body">
          <div className="kanban-card-actions">
            <button className="kanban-history-btn" onClick={loadHistory}>
              🕐 {showHistory ? 'Hide History' : 'History'}
            </button>
          </div>

          {/* History panel */}
          {showHistory && (
            <div className="kanban-history">
              {loadingHist ? (
                <div className="kanban-history-empty">Loading...</div>
              ) : history.length === 0 ? (
                <div className="kanban-history-empty">No moves recorded yet.</div>
              ) : (
                <div className="kanban-history-list">
                  {history.map((h, i) => (
                    <div key={i} className="kanban-history-entry">
                      <div className="kanban-history-move">
                        <span className="kanban-history-from">{h.from_status || '—'}</span>
                        <span className="kanban-history-arrow">→</span>
                        <span className="kanban-history-to">{h.to_status}</span>
                      </div>
                      <div className="kanban-history-when">
                        {h.changed_at.replace('T',' ').replace('Z',' UTC')}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Fail type selector — only in Failed lane */}
          {card.status === 'Failed' && (
            <div className="kanban-move">
              <label className="kanban-move-label">Fail Type:</label>
              <select
                className="kanban-move-select"
                value={card.fail_type || ''}
                onChange={e => setFailType(e.target.value)}
              >
                <option value="">Select...</option>
                {FAIL_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
          )}

          {/* Move dropdown */}
          <div className="kanban-move">
            <label className="kanban-move-label">Move to:</label>
            <select
              className="kanban-move-select"
              value=""
              onChange={e => { if (e.target.value) onMove(card.id, e.target.value) }}
            >
              <option value="">Select lane...</option>
              {LANES.filter((_, i) => i !== currentIdx).map(lane => (
                <option key={lane} value={lane}>{lane}</option>
              ))}
            </select>
          </div>

          {/* Notes */}
          <div className="kanban-notes">
            <div className="kanban-notes-header">
              <span className="kanban-move-label">Notes</span>
              <span className="kanban-notes-count">{notes.length}</span>
            </div>

            {/* Existing notes feed */}
            {notes.length > 0 && (
              <div className="kanban-notes-feed">
                {notes.map(n => (
                  <div key={n.id} className="kanban-note-entry">
                    <div className="kanban-note-text">{n.note}</div>
                    <div className="kanban-note-footer">
                      <span className="kanban-note-time">
                        {n.created_at.replace('T',' ').replace('Z',' UTC')}
                      </span>
                      <button
                        className="kanban-note-delete"
                        onClick={() => deleteNote(n.id)}
                        title="Delete note"
                      >✕</button>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {/* Add new note */}
            <textarea
              className="kanban-note-input"
              value={newNote}
              onChange={e => setNewNote(e.target.value)}
              rows={2}
              placeholder="Add a note..."
              onKeyDown={e => { if (e.key === 'Enter' && e.ctrlKey) saveNote() }}
            />
            <button
              className="btn-save-sm"
              onClick={saveNote}
              disabled={saving || !newNote.trim()}
            >
              {saving ? 'Saving...' : 'Add Note'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}

function FailTypeDialog({ onConfirm, onCancel }) {
  const [selected, setSelected] = useState('')

  return (
    <div className="fail-dialog-overlay" onClick={onCancel}>
      <div className="fail-dialog" onClick={e => e.stopPropagation()}>
        <h3>Move to Failed</h3>
        <p>Select the reason this application failed:</p>
        <div className="fail-dialog-options">
          {FAIL_TYPES.map(t => (
            <label key={t} className={`fail-dialog-option ${selected === t ? 'selected' : ''}`}>
              <input
                type="radio"
                name="fail_type"
                value={t}
                checked={selected === t}
                onChange={() => setSelected(t)}
              />
              {t}
            </label>
          ))}
        </div>
        <div className="fail-dialog-actions">
          <button
            className="btn-run"
            onClick={() => selected && onConfirm(selected)}
            disabled={!selected}
          >
            Confirm
          </button>
          <button className="btn-cancel-sm" onClick={onCancel}>Cancel</button>
        </div>
      </div>
    </div>
  )
}

export default function Kanban({ anonymize }) {
  const [cards, setCards]     = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')
  const [search, setSearch]   = useState('')
  const dragCardId            = useRef(null)
  const [dragOverLane, setDragOverLane] = useState(null)
  const boardRef    = useRef(null)
  const headersRef  = useRef(null)

  const syncScroll = (e) => {
    if (headersRef.current) headersRef.current.scrollLeft = e.target.scrollLeft
  }

  const fetchCards = useCallback(async () => {
    try {
      const res = await fetch(`${API}/kanban`)
      setCards(await res.json())
    } catch {
      setError('Could not reach API.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { fetchCards() }, [fetchCards])

  const [failDialog, setFailDialog] = useState(null) // { id, lane } when pending

  const moveCard = async (id, status, failType = null) => {
    await fetch(`${API}/kanban/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    })
    if (failType) {
      await fetch(`${API}/kanban/${id}/fail-type`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fail_type: failType }),
      })
    }
    fetchCards()
  }

  const handleDrop = async (lane) => {
    if (dragCardId.current && lane) {
      const card = cards.find(c => c.id === dragCardId.current)
      if (card && card.status !== lane) {
        if (lane === 'Failed') {
          setFailDialog({ id: dragCardId.current, lane })
        } else {
          await moveCard(dragCardId.current, lane)
        }
      }
    }
    dragCardId.current = null
    setDragOverLane(null)
  }

  const handleMoveCard = async (id, lane) => {
    if (lane === 'Failed') {
      setFailDialog({ id, lane })
    } else {
      await moveCard(id, lane)
    }
  }

  const filtered = search
    ? cards.filter(c =>
        c.title?.toLowerCase().includes(search.toLowerCase()) ||
        c.company?.toLowerCase().includes(search.toLowerCase())
      )
    : cards

  const byLane = (lane) => filtered.filter(c => c.status === lane)

  if (loading) return <div className="loading">Loading Kanban board...</div>

  return (
    <div className="kanban-page">
      {/* Sticky header — contains title, search AND lane headers */}
      <div className="kanban-header">
        <div className="kanban-header-top">
          <h2>Job Pipeline</h2>
          <input
            className="kanban-search"
            type="text"
            placeholder="Filter by title or company..."
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
          <span className="kanban-total">{cards.length} total jobs</span>
        </div>
        <div className="kanban-lane-headers-row" ref={headersRef}>
          {LANES.map(lane => {
            const count = byLane(lane).length
            return (
              <div
                key={lane}
                className="kanban-lane-header"
                style={{ borderTopColor: LANE_COLORS[lane] }}
              >
                <span className="kanban-lane-title">{lane}</span>
                <span className="kanban-lane-count">{count}</span>
              </div>
            )
          })}
        </div>
      </div>

      {error && <div className="msg error">{error}</div>}

      {/* Board — only card bodies, no lane headers */}
      <div className="kanban-board" ref={boardRef} onScroll={syncScroll}>
        {LANES.map(lane => {
          const laneCards = byLane(lane)
          return (
            <div
              key={lane}
              className={`kanban-lane ${dragOverLane === lane ? 'kanban-lane-drag-over' : ''}`}
              onDragOver={e => { e.preventDefault(); e.stopPropagation(); setDragOverLane(lane) }}
              onDragEnter={e => { e.preventDefault(); setDragOverLane(lane) }}
              onDragLeave={e => {
                // Only clear if leaving the lane entirely, not just moving over a child
                if (!e.currentTarget.contains(e.relatedTarget)) setDragOverLane(null)
              }}
              onDrop={e => { e.preventDefault(); handleDrop(lane) }}
            >
              <div className="kanban-lane-body">
                {laneCards.length === 0
                  ? <div className="kanban-empty">Drop here</div>
                  : laneCards.map(card => (
                      <Card
                        key={card.id}
                        card={card}
                        onMove={handleMoveCard}
                        onSave={fetchCards}
                        onDragStart={id => { dragCardId.current = id }}
                        anonymize={anonymize}
                      />
                    ))
                }
              </div>
            </div>
          )
        })}
      </div>

      {/* Fail type dialog */}
      {failDialog && (
        <FailTypeDialog
          onConfirm={async (failType) => {
            await moveCard(failDialog.id, failDialog.lane, failType)
            setFailDialog(null)
          }}
          onCancel={() => setFailDialog(null)}
        />
      )}
    </div>
  )
}
