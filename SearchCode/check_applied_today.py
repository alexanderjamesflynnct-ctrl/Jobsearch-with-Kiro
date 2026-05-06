import sqlite3
from pathlib import Path
from datetime import datetime, timezone

conn = sqlite3.connect(Path(__file__).parent / "jobs.db")

print("=== Last 10 'Applied' history entries ===")
rows = conn.execute(
    "SELECT kanban_id, to_status, changed_at FROM kanban_history WHERE to_status = 'Applied' ORDER BY changed_at DESC LIMIT 10"
).fetchall()
for r in rows:
    print(f"  kanban_id={r[0]}  status={r[1]}  at={r[2]}")

today_utc = datetime.now(timezone.utc).strftime("%Y-%m-%d")
print(f"\n=== Today (UTC): {today_utc} ===")
count = conn.execute(
    "SELECT COUNT(DISTINCT kanban_id) FROM kanban_history WHERE to_status = 'Applied' AND changed_at LIKE ?",
    (f"{today_utc}%",)
).fetchone()[0]
print(f"Applied today (UTC): {count}")

conn.close()
