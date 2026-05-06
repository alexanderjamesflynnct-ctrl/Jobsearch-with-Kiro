import sqlite3
from pathlib import Path

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")
cols = [r[1] for r in conn.execute("PRAGMA table_info(kanban_jobs)")]
if "is_active" not in cols:
    conn.execute("ALTER TABLE kanban_jobs ADD COLUMN is_active INTEGER NOT NULL DEFAULT 0")
    conn.commit()
    print("Added is_active column.")
else:
    print("Column already exists.")
conn.close()
