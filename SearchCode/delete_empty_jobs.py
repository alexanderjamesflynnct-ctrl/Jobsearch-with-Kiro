import sqlite3
from pathlib import Path

db = Path(__file__).parent / "jobs.db"
conn = sqlite3.connect(db)
cur = conn.execute(
    "DELETE FROM job_listings WHERE "
    "(title IS NULL OR title = '') AND "
    "(company IS NULL OR company = '') AND "
    "(location IS NULL OR location = '')"
)
conn.commit()
print(f"Deleted {cur.rowcount} empty job listing(s)")

# Also reset the job_links so they can be re-imported once scraping is fixed
cur2 = conn.execute("UPDATE job_links SET processed = 0, processed_at = NULL WHERE processed = 1")
conn.commit()
print(f"Reset {cur2.rowcount} link(s) back to pending")
conn.close()
