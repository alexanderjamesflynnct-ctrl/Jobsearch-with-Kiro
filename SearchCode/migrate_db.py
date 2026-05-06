"""Run once to add city and state columns to existing jobs.db"""
import sqlite3
from pathlib import Path

db = Path(__file__).parent / "jobs.db"
conn = sqlite3.connect(db)
cols = [r[1] for r in conn.execute("PRAGMA table_info(job_listings)")]

added = []
if "city" not in cols:
    conn.execute("ALTER TABLE job_listings ADD COLUMN city TEXT")
    added.append("city")
if "state" not in cols:
    conn.execute("ALTER TABLE job_listings ADD COLUMN state TEXT")
    added.append("state")

conn.commit()
conn.close()

if added:
    print(f"Added columns: {', '.join(added)}")
else:
    print("Columns already exist, nothing to do.")
