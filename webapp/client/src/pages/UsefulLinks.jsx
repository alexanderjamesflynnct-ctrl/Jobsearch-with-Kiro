import { useState, useEffect, useCallback } from 'react'

const API = 'http://localhost:8000'

export default function UsefulLinks() {
  const [links, setLinks]       = useState([])
  const [url, setUrl]           = useState('')
  const [desc, setDesc]         = useState('')
  const [error, setError]       = useState('')
  const [status, setStatus]     = useState('')
  const [editId, setEditId]     = useState(null)
  const [editUrl, setEditUrl]   = useState('')
  const [editDesc, setEditDesc] = useState('')

  const fetchLinks = useCallback(async () => {
    try {
      const res = await fetch(`${API}/useful-links`)
      setLinks(await res.json())
    } catch {
      setError('Could not reach API.')
    }
  }, [])

  useEffect(() => { fetchLinks() }, [fetchLinks])

  const addLink = async () => {
    setError(''); setStatus('')
    if (!url.trim())  return setError('URL is required.')
    if (!desc.trim()) return setError('Description is required.')

    const res  = await fetch(`${API}/useful-links`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url: url.trim(), description: desc.trim() }),
    })
    const data = await res.json()
    if (!res.ok) { setError(data.error || 'Failed to add.'); return }
    setStatus('Link added.')
    setUrl(''); setDesc('')
    fetchLinks()
  }

  const deleteLink = async (id) => {
    await fetch(`${API}/useful-links/${id}`, { method: 'DELETE' })
    fetchLinks()
  }

  const startEdit = (link) => {
    setEditId(link.id)
    setEditUrl(link.url)
    setEditDesc(link.description)
  }

  const saveEdit = async () => {
    await fetch(`${API}/useful-links/${editId}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url: editUrl, description: editDesc }),
    })
    setEditId(null)
    fetchLinks()
  }

  return (
    <div className="useful-links-page">
      <h2>Useful Links</h2>
      <p className="subtitle">Store links to job boards, salary tools, company research pages, and anything else useful.</p>

      {/* Add form */}
      <div className="ul-form">
        <input
          type="text"
          placeholder="Description (e.g. LinkedIn Salary Insights)"
          value={desc}
          onChange={e => setDesc(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && addLink()}
        />
        <input
          type="text"
          placeholder="https://..."
          value={url}
          onChange={e => setUrl(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && addLink()}
        />
        <button className="btn-primary" onClick={addLink}>Add Link</button>
      </div>

      {error  && <div className="msg error">{error}</div>}
      {status && <div className="msg success">{status}</div>}

      {/* Table */}
      {links.length === 0 ? (
        <p className="empty">No links saved yet. Add one above.</p>
      ) : (
        <table className="ul-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Description</th>
              <th>URL</th>
              <th>Added</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {links.map(link => (
              <tr key={link.id}>
                <td className="ul-id">{link.id}</td>
                {editId === link.id ? (
                  <>
                    <td>
                      <input
                        className="ul-edit-input"
                        value={editDesc}
                        onChange={e => setEditDesc(e.target.value)}
                      />
                    </td>
                    <td>
                      <input
                        className="ul-edit-input"
                        value={editUrl}
                        onChange={e => setEditUrl(e.target.value)}
                      />
                    </td>
                    <td className="date">{link.added_at?.replace('T',' ').replace('Z','')}</td>
                    <td className="action-cell">
                      <button className="btn-save-sm" onClick={saveEdit}>Save</button>
                      <button className="btn-cancel-sm" onClick={() => setEditId(null)}>✕</button>
                    </td>
                  </>
                ) : (
                  <>
                    <td className="ul-desc">{link.description}</td>
                    <td className="ul-url">
                      <a href={link.url} target="_blank" rel="noreferrer">{link.url}</a>
                    </td>
                    <td className="date">{link.added_at?.replace('T',' ').replace('Z','')}</td>
                    <td className="action-cell">
                      <button className="btn-reset-sm" onClick={() => startEdit(link)} title="Edit">✎</button>
                      <button className="btn-delete"   onClick={() => deleteLink(link.id)}>✕</button>
                    </td>
                  </>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
