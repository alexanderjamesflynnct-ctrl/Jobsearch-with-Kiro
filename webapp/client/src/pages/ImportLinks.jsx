import { useState, useEffect, useCallback } from 'react'
import Bookmarklet from '../components/Bookmarklet'

const API = 'http://localhost:8000'

export default function ImportLinks() {
  const [links, setLinks]           = useState([])
  const [input, setInput]           = useState('')
  const [error, setError]           = useState('')
  const [status, setStatus]         = useState('')
  const [processing, setProcessing] = useState(false)
  const [progress, setProgress]     = useState([])   // per-job progress lines

  const fetchLinks = useCallback(async () => {
    try {
      const res = await fetch(`${API}/links`)
      setLinks(await res.json())
    } catch {
      setError('Could not reach API.')
    }
  }, [])

  useEffect(() => { fetchLinks() }, [fetchLinks])

  const addLinks = async () => {
    const urls = input.trim().split('\n').map(l => l.trim()).filter(l => l)
    if (!urls.length) return
    setError('')
    setStatus('')

    let added = 0, skipped = 0, errors = []
    for (const url of urls) {
      const res = await fetch(`${API}/links`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url }),
      })
      const data = await res.json()
      if (!res.ok) {
        errors.push(`${url.slice(0, 60)}: ${data.error}`)
      } else if (data.message === 'Link already exists') {
        skipped++
      } else {
        added++
      }
    }

    const parts = []
    if (added)   parts.push(`${added} added`)
    if (skipped) parts.push(`${skipped} already existed`)
    if (errors.length) parts.push(`${errors.length} failed`)
    setStatus(parts.join(', '))
    if (errors.length) setError(errors.join('\n'))
    setInput('')
    fetchLinks()
  }

  const deleteLink = async (id) => {
    await fetch(`${API}/links/${id}`, { method: 'DELETE' })
    fetchLinks()
  }

  const resetLink = async (id) => {
    await fetch(`${API}/links/${id}/reset`, { method: 'POST' })
    fetchLinks()
  }

  const clearImported = async () => {
    if (!window.confirm(`Delete all ${processed.length} imported link(s)?`)) return
    await fetch(`${API}/links/all`, { method: 'DELETE' })
    setStatus('Cleared all imported links.')
    fetchLinks()
  }

  const resetAll = async () => {
    await fetch(`${API}/links/reset-all`, { method: 'POST' })
    setStatus('All links reset to pending.')
    fetchLinks()
  }

  const processLinks = async () => {
    setProcessing(true)
    setStatus('')
    setError('')
    setProgress([])

    // Seed progress rows for each pending link
    const pending = links.filter(l => !l.processed)
    setProgress(pending.map(l => ({ url: l.url, source: l.source, state: 'pending', result: '' })))

    // Animate spinner while waiting
    let tick = 0
    const spinnerInterval = setInterval(() => {
      tick++
      setProgress(prev => prev.map(p =>
        p.state === 'pending' ? { ...p, tick } : p
      ))
    }, 300)

    try {
      const res  = await fetch(`${API}/links/process`, { method: 'POST' })
      const data = await res.json()
      clearInterval(spinnerInterval)

      if (!res.ok) {
        const detail = data.errors || data.output || data.error || 'Unknown error'
        setError(`Process failed:\n${detail}`)
        setProgress(prev => prev.map(p => ({ ...p, state: 'error', result: 'Failed' })))
      } else {
        // Parse output lines to match results back to URLs
        const lines = data.lines || []
        setProgress(prev => prev.map((p, i) => {
          const okLine  = lines.find(l => l.startsWith('OK:') && i < lines.length)
          const fetched = lines.find(l => l.includes('Fetching') && l.includes(extractId(p.url)))
          const result  = lines.find((l, li) => {
            const prevLine = lines[li - 1] || ''
            return l.startsWith('OK:') && prevLine.includes(extractId(p.url))
          })
          return {
            ...p,
            state:  'done',
            result: result ? result.replace('OK:', '').trim() : (okLine ? 'Imported' : 'Imported'),
          }
        }))
        setStatus(`Done: ${data.processed} link(s) imported successfully.`)
        fetchLinks()
      }
    } catch (e) {
      clearInterval(spinnerInterval)
      setError('Process request failed: ' + e.message)
      setProgress(prev => prev.map(p => ({ ...p, state: 'error', result: 'Failed' })))
    } finally {
      setProcessing(false)
    }
  }

  const extractId = (url) => {
    const m = url.match(/\/(\d+)\/?/)
    return m ? m[1] : url.slice(-12)
  }

  const pending   = links.filter(l => !l.processed)
  const processed = links.filter(l =>  l.processed)
  const spinChars = ['⠋','⠙','⠹','⠸','⠼','⠴','⠦','⠧','⠇','⠏']

  return (
    <div className="import-page">
      <h2>Import Job Links</h2>
      <p className="subtitle">Paste LinkedIn, Indeed, Glassdoor or ZipRecruiter job URLs to queue them for import.</p>

      <Bookmarklet />

      <div className="link-input-row">
        <textarea
          className="link-input-multi"
          placeholder="Paste one or more LinkedIn, Indeed, Glassdoor or ZipRecruiter job URLs (one per line)..."
          value={input}
          onChange={e => setInput(e.target.value)}
          rows={3}
        />
        <div className="link-input-buttons">
          <button className="btn-primary" onClick={addLinks}>Add Link(s)</button>
          <span className="link-input-hint">{input.trim() ? `${input.trim().split('\n').filter(l => l.trim()).length} URL(s)` : ''}</span>
        </div>
      </div>

      {error  && <div className="msg error"><pre>{error}</pre></div>}
      {status && <div className="msg success">{status}</div>}

      {/* Progress view while processing */}
      {progress.length > 0 && (
        <div className="progress-panel">
          <h3>Import Progress</h3>
          <table className="link-table">
            <thead>
              <tr><th>Source</th><th>URL</th><th>Status</th><th>Result</th></tr>
            </thead>
            <tbody>
              {progress.map((p, i) => (
                <tr key={i} className={`progress-row progress-${p.state}`}>
                  <td><span className={`badge badge-${p.source}`}>{p.source}</span></td>
                  <td className="url-cell">
                    <a href={p.url} target="_blank" rel="noreferrer">{cleanUrl(p.url)}</a>
                  </td>
                  <td className="status-cell">
                    {p.state === 'pending'  && <span className="spinner">{spinChars[(p.tick || 0) % spinChars.length]}</span>}
                    {p.state === 'done'     && <span className="status-done">Done</span>}
                    {p.state === 'error'    && <span className="status-error">Error</span>}
                  </td>
                  <td className="result-cell">{p.result}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Pending queue */}
      {progress.length === 0 && (
        <>
          <div className="section-header">
            <h3>Pending ({pending.length})</h3>
            <button
              className="btn-run"
              onClick={processLinks}
              disabled={processing || pending.length === 0}
            >
              {processing ? 'Processing...' : `▶ Run Import (${pending.length})`}
            </button>
          </div>

          {pending.length === 0 ? (
            <p className="empty">No pending links. Paste a URL above to get started.</p>
          ) : (
            <table className="link-table">
              <thead>
                <tr><th>Source</th><th>URL</th><th>Added</th><th></th></tr>
              </thead>
              <tbody>
                {pending.map(link => (
                  <tr key={link.id}>
                    <td><span className={`badge badge-${link.source}`}>{link.source}</span></td>
                    <td className="url-cell">
                      <a href={link.url} target="_blank" rel="noreferrer">{cleanUrl(link.url)}</a>
                    </td>
                    <td className="date">{link.added_at?.replace('T',' ').replace('Z','')}</td>
                    <td>
                      <button className="btn-delete" onClick={() => deleteLink(link.id)}>✕</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}

      {/* After processing — show run again button */}
      {progress.length > 0 && !processing && (
        <div className="section-header" style={{marginTop: '16px'}}>
          <button className="btn-run" onClick={() => { setProgress([]); fetchLinks() }}>
            ← Back to Queue
          </button>
        </div>
      )}

      {/* Processed */}
      {processed.length > 0 && progress.length === 0 && (
        <>
          <div className="section-header">
            <h3 className="processed-header">Processed ({processed.length})</h3>
            <div style={{display:'flex', gap:'8px'}}>
              <button className="btn-reset" onClick={resetAll}>↺ Reset All to Pending</button>
              <button className="btn-clear-all" onClick={clearImported}>🗑 Clear All Imported</button>
            </div>
          </div>
          <table className="link-table processed">
            <thead>
              <tr><th>Source</th><th>URL</th><th>Added</th><th>Processed</th><th>Status</th><th></th></tr>
            </thead>
            <tbody>
              {processed.map(link => {
                const msg = link.error_message || ''
                const isDuplicate = msg.startsWith('Already imported')
                const isFailed = msg && !isDuplicate
                return (
                  <tr key={link.id} className={isFailed ? 'link-row-failed' : isDuplicate ? 'link-row-duplicate' : ''} title={msg}>
                    <td><span className={`badge badge-${link.source}`}>{link.source}</span></td>
                    <td className="url-cell">
                      <a href={link.url} target="_blank" rel="noreferrer">{cleanUrl(link.url)}</a>
                    </td>
                    <td className="date">{link.added_at?.replace('T',' ').replace('Z','')}</td>
                    <td className="date">{link.processed_at?.replace('T',' ').replace('Z','')}</td>
                    <td>{isFailed
                      ? <span className="link-fail-badge" title={msg}>Failed</span>
                      : isDuplicate
                      ? <span className="link-dup-badge" title={msg}>Duplicate</span>
                      : <span className="link-success-badge">OK</span>
                    }</td>
                    <td className="action-cell">
                      <button className="btn-reset-sm" onClick={() => resetLink(link.id)} title="Reset to pending">↺</button>
                      <button className="btn-delete"   onClick={() => deleteLink(link.id)}>✕</button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </>
      )}
    </div>
  )
}

function cleanUrl(url) {
  // Show just the clean job ID portion, not the full tracking URL
  try {
    const u = new URL(url)
    return u.hostname + u.pathname
  } catch {
    return url.slice(0, 80)
  }
}
