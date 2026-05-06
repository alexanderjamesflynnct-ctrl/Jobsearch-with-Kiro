import sqlite3
from pathlib import Path

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")
cur = conn.execute(
    "UPDATE kanban_jobs SET status = 'Researching' WHERE status = 'Researched'"
)
conn.commit()
print(f"Updated {cur.rowcount} row(s)")
conn.close()
