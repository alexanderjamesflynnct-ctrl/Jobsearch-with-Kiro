"""
Backfill: add an 'Applied' history entry for all Failed jobs that don't already have one.
"""
import sqlite3
from pathlib import Path

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")

# Find Failed kanban_jobs that have no 'Applied' history entry
rows = conn.execute("""
    SELECT k.id, k.updated_at FROM kanban_jobs k
    WHERE k.status = 'Failed'
      AND k.id NOT IN (
          SELECT DISTINCT kanban_id FROM kanban_history WHERE to_status = 'Applied'
      )
""").fetchall()

for kid, updated_at in rows:
    conn.execute(
        "INSERT INTO kanban_history (kanban_id, from_status, to_status, changed_at) VALUES (?,?,?,?)",
        (kid, "Searched/Found", "Applied", updated_at)
    )

conn.commit()
print(f"Backfilled {len(rows)} 'Applied' history entries for Failed jobs.")
conn.close()
