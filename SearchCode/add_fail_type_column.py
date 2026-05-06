import sqlite3
from pathlib import Path

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")
cols = [r[1] for r in conn.execute("PRAGMA table_info(kanban_jobs)")]

if "fail_type" not in cols:
    conn.execute("ALTER TABLE kanban_jobs ADD COLUMN fail_type TEXT")
    # Migrate old lane names to Failed + fail_type
    migrations = {
        "Not Heard Back (30d)":             "Not Heard Back (30d)",
        "Application Error":                "Application Error",
        "Rejected by Employer":             "Rejected by Employer",
        "No Longer Accepting Applications": "No Longer Accepting Applications",
    }
    for old_status, fail_type in migrations.items():
        conn.execute(
            "UPDATE kanban_jobs SET status = 'Failed', fail_type = ? WHERE status = ?",
            (fail_type, old_status)
        )
    conn.commit()
    print("Added fail_type column and migrated old lanes.")
else:
    print("Column already exists.")
conn.close()
