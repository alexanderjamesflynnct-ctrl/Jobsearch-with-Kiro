"""One-time migration: add source column to job_listings."""
import sqlite3
from pathlib import Path

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")
cols = [r[1] for r in conn.execute("PRAGMA table_info(job_listings)")]

if "source" not in cols:
    conn.execute("ALTER TABLE job_listings ADD COLUMN source TEXT")
    # Back-fill existing rows based on URL
    conn.execute("UPDATE job_listings SET source = 'linkedin'    WHERE url LIKE '%linkedin.com%'")
    conn.execute("UPDATE job_listings SET source = 'indeed'      WHERE url LIKE '%indeed.com%'")
    conn.execute("UPDATE job_listings SET source = 'glassdoor'   WHERE url LIKE '%glassdoor.com%'")
    conn.execute("UPDATE job_listings SET source = 'ziprecruiter' WHERE url LIKE '%ziprecruiter.com%'")
    conn.execute("UPDATE job_listings SET source = 'adzuna'      WHERE source IS NULL")
    conn.commit()
    print("Added and back-filled source column.")
else:
    print("source column already exists.")

conn.close()
