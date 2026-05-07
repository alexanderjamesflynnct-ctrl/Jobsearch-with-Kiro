import sqlite3
from pathlib import Path

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")
cols = [r[1] for r in conn.execute("PRAGMA table_info(job_links)")]
if "error_message" not in cols:
    conn.execute("ALTER TABLE job_links ADD COLUMN error_message TEXT")
    conn.commit()
    print("Added error_message column to job_links.")
else:
    print("Column already exists.")
conn.close()
