import { useState, useEffect, useCallback } from "react";
import Bookmarklet from "../components/Bookmarklet";

const API = "http://localhost:5300/api/Links"; // Updated to Controller Path

export default function ImportLinks() {
  const [links, setLinks] = useState([]);
  const [input, setInput] = useState("");
  const [error, setError] = useState("");
  const [status, setStatus] = useState("");
  const [processing, setProcessing] = useState(false);
  const [progress, setProgress] = useState([]);

  const fetchLinks = useCallback(async () => {
    try {
      const res = await fetch(`${API}`); // GET /api/Links
      setLinks(await res.json());
    } catch {
      setError("Could not reach API.");
    }
  }, []);

  useEffect(() => {
    fetchLinks();
  }, [fetchLinks]);

  const addLinks = async () => {
    const urls = input
      .trim()
      .split("\n")
      .map((l) => l.trim())
      .filter((l) => l);
    if (!urls.length) return;
    setError("");
    setStatus("");

    let added = 0,
      skipped = 0,
      errors = [];
    for (const url of urls) {
      const res = await fetch(`${API}`, {
        // POST /api/Links
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ url }),
      });
      const data = await res.json();
      if (!res.ok) {
        errors.push(`${url.slice(0, 60)}: ${data.error || "Unknown Error"}`);
      } else if (data.message === "Link already exists") {
        skipped++;
      } else {
        added++;
      }
    }

    const parts = [];
    if (added) parts.push(`${added} added`);
    if (skipped) parts.push(`${skipped} already existed`);
    if (errors.length) parts.push(`${errors.length} failed`);
    setStatus(parts.join(", "));
    if (errors.length) setError(errors.join("\n"));
    setInput("");
    fetchLinks();
  };

  const deleteLink = async (id) => {
    await fetch(`${API}/${id}`, { method: "DELETE" }); // DELETE /api/Links/{id}
    fetchLinks();
  };

  const resetLink = async (id) => {
    await fetch(`${API}/${id}/reset`, { method: "POST" }); // POST /api/Links/{id}/reset
    fetchLinks();
  };

  const clearImported = async () => {
    if (!window.confirm(`Delete all ${processed.length} imported link(s)?`))
      return;
    // Updated route name to match the C# refactor 'all-processed'
    await fetch(`${API}/all-processed`, { method: "DELETE" });
    setStatus("Cleared all imported links.");
    fetchLinks();
  };

  const resetAll = async () => {
    await fetch(`${API}/reset-all`, { method: "POST" }); // POST /api/Links/reset-all
    setStatus("All links reset to pending.");
    fetchLinks();
  };

  const processLinks = async () => {
    setProcessing(true);
    setStatus("");
    setError("");
    setProgress([]);

    const pending = links.filter((l) => !l.processed);
    setProgress(
      pending.map((l) => ({
        url: l.url,
        source: l.source,
        state: "pending",
        result: "",
      })),
    );

    let tick = 0;
    const spinnerInterval = setInterval(() => {
      tick++;
      setProgress((prev) =>
        prev.map((p) => (p.state === "pending" ? { ...p, tick } : p)),
      );
    }, 300);

    try {
      const res = await fetch(`${API}/process`, { method: "POST" }); // POST /api/Links/process
      const data = await res.json();
      clearInterval(spinnerInterval);

      if (!res.ok) {
        const detail =
          data.errors || data.output || data.error || "Unknown error";
        setError(`Process failed:\n${detail}`);
        setProgress((prev) =>
          prev.map((p) => ({ ...p, state: "error", result: "Failed" })),
        );
      } else {
        const lines = data.lines || [];
        setProgress((prev) =>
          prev.map((p, i) => {
            const result = lines.find((l, li) => {
              const prevLine = lines[li - 1] || "";
              return l.startsWith("OK:") && prevLine.includes(extractId(p.url));
            });
            return {
              ...p,
              state: "done",
              result: result ? result.replace("OK:", "").trim() : "Imported",
            };
          }),
        );
        setStatus(`Done: ${data.processed} link(s) processed.`);
        fetchLinks();
      }
    } catch (e) {
      clearInterval(spinnerInterval);
      setError("Process request failed: " + e.message);
      setProgress((prev) =>
        prev.map((p) => ({ ...p, state: "error", result: "Failed" })),
      );
    } finally {
      setProcessing(false);
    }
  };

  const extractId = (url) => {
    const m = url.match(/\/(\d+)\/?/);
    return m ? m[1] : url.slice(-12);
  };

  const pending = links.filter((l) => !l.processed);
  const processed = links.filter((l) => l.processed);
  const spinChars = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

  return (
    <div className="import-page">
      <h2>Import Job Links</h2>
      <p className="subtitle">
        Paste LinkedIn, Indeed, Glassdoor or ZipRecruiter job URLs to queue them
        for import.
      </p>

      <Bookmarklet />

      <div className="link-input-row">
        <textarea
          className="link-input-multi"
          placeholder="Paste job URLs..."
          value={input}
          onChange={(e) => setInput(e.target.value)}
          rows={3}
        />
        <div className="link-input-buttons">
          <button className="btn-primary" onClick={addLinks}>
            Add Link(s)
          </button>
        </div>
      </div>

      {error && (
        <div className="msg error">
          <pre>{error}</pre>
        </div>
      )}
      {status && <div className="msg success">{status}</div>}

      {progress.length > 0 && (
        <div className="progress-panel">
          <h3>Import Progress</h3>
          <table className="link-table">
            <thead>
              <tr>
                <th>Source</th>
                <th>URL</th>
                <th>Status</th>
                <th>Result</th>
              </tr>
            </thead>
            <tbody>
              {progress.map((p, i) => (
                <tr key={i} className={`progress-row progress-${p.state}`}>
                  <td>
                    <span className={`badge badge-${p.source}`}>
                      {p.source}
                    </span>
                  </td>
                  <td className="url-cell">
                    <a href={p.url} target="_blank" rel="noreferrer">
                      {cleanUrl(p.url)}
                    </a>
                  </td>
                  <td className="status-cell">
                    {p.state === "pending" && (
                      <span className="spinner">
                        {spinChars[(p.tick || 0) % spinChars.length]}
                      </span>
                    )}
                    {p.state === "done" && (
                      <span className="status-done">Done</span>
                    )}
                    {p.state === "error" && (
                      <span className="status-error">Error</span>
                    )}
                  </td>
                  <td className="result-cell">{p.result}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {progress.length === 0 && (
        <>
          <div className="section-header">
            <h3>Pending ({pending.length})</h3>
            <button
              className="btn-run"
              onClick={processLinks}
              disabled={processing || pending.length === 0}
            >
              {processing
                ? "Processing..."
                : `▶ Run Import (${pending.length})`}
            </button>
          </div>
          {pending.length === 0 ? (
            <p className="empty">No pending links.</p>
          ) : (
            <table className="link-table">
              <thead>
                <tr>
                  <th>Source</th>
                  <th>URL</th>
                  <th>Added</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {pending.map((link) => (
                  <tr key={link.id}>
                    <td>
                      <span className={`badge badge-${link.source}`}>
                        {link.source}
                      </span>
                    </td>
                    <td className="url-cell">
                      <a href={link.url} target="_blank" rel="noreferrer">
                        {cleanUrl(link.url)}
                      </a>
                    </td>
                    <td className="date">
                      {link.added_at?.replace("T", " ").replace("Z", "")}
                    </td>
                    <td>
                      <button
                        className="btn-delete"
                        onClick={() => deleteLink(link.id)}
                      >
                        ✕
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}

      {processed.length > 0 && progress.length === 0 && (
        <>
          <div className="section-header">
            <h3 className="processed-header">Processed ({processed.length})</h3>
            <div style={{ display: "flex", gap: "8px" }}>
              <button className="btn-reset" onClick={resetAll}>
                ↺ Reset All
              </button>
              <button className="btn-clear-all" onClick={clearImported}>
                🗑 Clear All
              </button>
            </div>
          </div>
          <table className="link-table processed">
            <thead>
              <tr>
                <th>Source</th>
                <th>URL</th>
                <th>Added</th>
                <th>Processed</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {processed.map((link) => {
                const msg = link.error_message || "";
                const isDuplicate = msg.startsWith("Already imported");
                return (
                  <tr
                    key={link.id}
                    className={
                      msg && !isDuplicate
                        ? "link-row-failed"
                        : isDuplicate
                          ? "link-row-duplicate"
                          : ""
                    }
                  >
                    <td>
                      <span className={`badge badge-${link.source}`}>
                        {link.source}
                      </span>
                    </td>
                    <td className="url-cell">
                      <a href={link.url} target="_blank" rel="noreferrer">
                        {cleanUrl(link.url)}
                      </a>
                    </td>
                    <td className="date">
                      {link.added_at?.replace("T", " ").replace("Z", "")}
                    </td>
                    <td className="date">
                      {link.processed_at?.replace("T", " ").replace("Z", "")}
                    </td>
                    <td>
                      {msg && !isDuplicate
                        ? "Failed"
                        : isDuplicate
                          ? "Dup"
                          : "OK"}
                    </td>
                    <td className="action-cell">
                      <button
                        className="btn-reset-sm"
                        onClick={() => resetLink(link.id)}
                      >
                        ↺
                      </button>
                      <button
                        className="btn-delete"
                        onClick={() => deleteLink(link.id)}
                      >
                        ✕
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}

function cleanUrl(url) {
  try {
    const u = new URL(url);
    return u.hostname + u.pathname;
  } catch {
    return url.slice(0, 80);
  }
}
