"""
Delete a search run and all its job listings from the database.
Usage:
  python SearchCode/delete_search.py          # lists all searches
  python SearchCode/delete_search.py 1        # deletes search ID 1
  python SearchCode/delete_search.py all      # deletes everything
"""
import sqlite3
import sys
from pathlib import Path

db = Path(__file__).parent / "jobs.db"
conn = sqlite3.connect(db)
conn.row_factory = sqlite3.Row
conn.execute("PRAGMA foreign_keys = ON")

searches = conn.execute("SELECT id, searched_at, keywords, country FROM searches ORDER BY id").fetchall()

if not searches:
    print("No searches in database.")
    conn.close()
    sys.exit(0)

# No argument — just list
if len(sys.argv) < 2:
    print(f"{'ID':<5} {'Searched At':<22} {'Keywords':<40} {'Country'}")
    print("-" * 80)
    for s in searches:
        count = conn.execute("SELECT COUNT(*) FROM job_listings WHERE search_id=?", (s["id"],)).fetchone()[0]
        print(f"{s['id']:<5} {s['searched_at']:<22} {s['keywords']:<40} {s['country'] or 'all'} ({count} jobs)")
    print("\nUsage: python SearchCode/delete_search.py <id|all>")
    conn.close()
    sys.exit(0)

arg = sys.argv[1].strip().lower()

if arg == "all":
    conn.execute("DELETE FROM job_listings")
    conn.execute("DELETE FROM searches")
    conn.commit()
    print("Deleted all searches and job listings.")

else:
    try:
        search_id = int(arg)
    except ValueError:
        print(f"Invalid argument '{arg}'. Use a search ID number or 'all'.")
        conn.close()
        sys.exit(1)

    match = conn.execute("SELECT * FROM searches WHERE id=?", (search_id,)).fetchone()
    if not match:
        print(f"No search found with ID {search_id}.")
        conn.close()
        sys.exit(1)

    job_count = conn.execute("SELECT COUNT(*) FROM job_listings WHERE search_id=?", (search_id,)).fetchone()[0]
    conn.execute("DELETE FROM job_listings WHERE search_id=?", (search_id,))
    conn.execute("DELETE FROM searches WHERE id=?", (search_id,))
    conn.commit()
    print(f"Deleted search ID {search_id} ('{match['keywords']}') and {job_count} job listing(s).")

conn.close()
