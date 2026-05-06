"""
Import prompts from CSV into the prompts_log SQLite table.
Run once after creating the table.
"""
import csv
import sqlite3
from pathlib import Path

DB_PATH = Path(__file__).parent / "jobs.db"
CSV_PATH = Path(__file__).parent.parent / "app_metadata" / "prompts.csv"

conn = sqlite3.connect(DB_PATH)

# Create table if not exists
conn.execute("""
    CREATE TABLE IF NOT EXISTS prompts_log (
        id       INTEGER PRIMARY KEY AUTOINCREMENT,
        sequence INTEGER NOT NULL,
        date     TEXT    NOT NULL,
        prompt   TEXT    NOT NULL,
        category TEXT    NOT NULL DEFAULT '',
        response TEXT    NOT NULL DEFAULT ''
    )
""")

# Clear existing data to avoid duplicates on re-run
conn.execute("DELETE FROM prompts_log")

# Read CSV and insert
with open(CSV_PATH, "r", encoding="utf-8") as f:
    reader = csv.reader(f)
    header = next(reader)  # skip header
    count = 0
    for row in reader:
        if not row or not row[0].strip():
            continue
        seq      = int(row[0].strip())
        date     = row[1].strip() if len(row) > 1 else ""
        prompt   = row[2].strip() if len(row) > 2 else ""
        category = row[3].strip() if len(row) > 3 else ""
        response = row[4].strip() if len(row) > 4 else ""
        conn.execute(
            "INSERT INTO prompts_log (sequence, date, prompt, category, response) VALUES (?,?,?,?,?)",
            (seq, date, prompt, category, response)
        )
        count += 1

conn.commit()
print(f"Imported {count} prompts into prompts_log table.")
conn.close()
