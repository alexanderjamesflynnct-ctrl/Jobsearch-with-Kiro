"""One-time migration: add is_remote column to job_listings."""
import sqlite3, re
from pathlib import Path

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")
cols = [r[1] for r in conn.execute("PRAGMA table_info(job_listings)")]

if "is_remote" not in cols:
    conn.execute("ALTER TABLE job_listings ADD COLUMN is_remote TEXT")
    # Back-fill existing rows from description/location/job_type
    rows = conn.execute("SELECT id, title, location, job_type, description FROM job_listings").fetchall()
    for row in rows:
        text = " ".join(str(v or "") for v in row[1:]).lower()
        if "remote" in text and "hybrid" not in text:
            val = "Remote"
        elif "hybrid" in text:
            val = "Hybrid"
        elif "on-site" in text or "onsite" in text or "in office" in text:
            val = "On-site"
        else:
            val = None
        if val:
            conn.execute("UPDATE job_listings SET is_remote = ? WHERE id = ?", (val, row[0]))
    conn.commit()
    print("Added and back-filled is_remote column.")
else:
    print("is_remote column already exists.")
conn.close()
