import sqlite3
from pathlib import Path

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")
cur = conn.execute(
    "UPDATE kanban_jobs SET status = 'Failed', fail_type = 'Rejected By Me' "
    "WHERE status = 'Rejected By Me'"
)
conn.commit()
print(f"Migrated {cur.rowcount} row(s) from 'Rejected By Me' to 'Failed'")
conn.close()
