"""
SQLite database layer for job search results.

Schema
------
searches
  id          INTEGER  PK  — unique search run
  searched_at TEXT         — ISO datetime the search was executed
  keywords    TEXT
  location    TEXT
  country     TEXT         — NULL means multi-country run

job_listings
  id              TEXT  PK  — Adzuna job ID (stable across searches)
  search_id       INTEGER   — FK → searches.id
  searched_at     TEXT      — copy from search for easy querying
  date_posted     TEXT      — date the job was posted (from API)
  country         TEXT
  title           TEXT
  company         TEXT
  location        TEXT
  job_type        TEXT
  salary          TEXT
  url             TEXT
  description     TEXT
"""

import sqlite3
from datetime import datetime, timezone
from pathlib import Path

DB_PATH = Path(__file__).parent / "jobs.db"


def _connect() -> sqlite3.Connection:
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn


def init_db() -> None:
    """Create tables if they don't exist yet."""
    with _connect() as conn:
        conn.executescript("""
            CREATE TABLE IF NOT EXISTS searches (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                searched_at TEXT    NOT NULL,
                keywords    TEXT    NOT NULL,
                location    TEXT    NOT NULL DEFAULT '',
                country     TEXT
            );

            CREATE TABLE IF NOT EXISTS job_listings (
                id          TEXT    PRIMARY KEY,
                search_id   INTEGER NOT NULL REFERENCES searches(id),
                searched_at TEXT    NOT NULL,
                date_posted TEXT,
                country     TEXT,
                title       TEXT,
                company     TEXT,
                location    TEXT,
                city        TEXT,
                state       TEXT,
                job_type    TEXT,
                salary      TEXT,
                url         TEXT,
                source      TEXT,
                is_remote   TEXT,
                description TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_listings_search_id
                ON job_listings(search_id);
            CREATE INDEX IF NOT EXISTS idx_listings_date_posted
                ON job_listings(date_posted);

            CREATE TABLE IF NOT EXISTS kanban_jobs (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                job_listing_id TEXT    NOT NULL UNIQUE REFERENCES job_listings(id),
                status         TEXT    NOT NULL DEFAULT 'Searched/Found',
                notes          TEXT,
                updated_at     TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_kanban_status ON kanban_jobs(status);

            CREATE TABLE IF NOT EXISTS job_links (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                url        TEXT    NOT NULL UNIQUE,
                source     TEXT    NOT NULL DEFAULT 'unknown',
                added_at   TEXT    NOT NULL,
                processed  INTEGER NOT NULL DEFAULT 0,
                processed_at TEXT
            );
        """)


def save_search(keywords: str, location: str, country: str | None) -> tuple[int, str]:
    """
    Record a new search run.
    Returns (search_id, searched_at) so callers can tag each job with it.
    """
    searched_at = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    with _connect() as conn:
        cur = conn.execute(
            "INSERT INTO searches (searched_at, keywords, location, country) VALUES (?,?,?,?)",
            (searched_at, keywords, location, country),
        )
        return cur.lastrowid, searched_at


def save_jobs(jobs: list[dict], search_id: int, searched_at: str) -> tuple[int, int]:
    """
    Insert jobs into job_listings.
    Skips duplicates (same Adzuna job ID) — uses INSERT OR IGNORE so
    re-running the same search won't create duplicate rows.

    Returns (inserted, skipped) counts.
    """
    inserted = skipped = 0
    with _connect() as conn:
        for job in jobs:
            cur = conn.execute(
                """
                INSERT OR IGNORE INTO job_listings
                    (id, search_id, searched_at, date_posted,
                     country, title, company, location, city, state,
                     job_type, salary, url, source, is_remote, description)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                """,
                (
                    job["adzuna_id"],
                    search_id,
                    searched_at,
                    job.get("posted") or None,
                    job.get("country"),
                    job.get("title"),
                    job.get("company"),
                    job.get("location"),
                    job.get("city"),
                    job.get("state"),
                    job.get("job_type"),
                    job.get("salary"),
                    job.get("url"),
                    job.get("source"),
                    job.get("is_remote") or None,
                    job.get("description"),
                ),
            )
            if cur.rowcount:
                inserted += 1
                # Auto-create kanban card at "Searched/Found"
                conn.execute(
                    "INSERT OR IGNORE INTO kanban_jobs (job_listing_id, status, updated_at) VALUES (?,?,?)",
                    (job["adzuna_id"], "Searched/Found", searched_at),
                )
            else:
                skipped += 1

    return inserted, skipped


def query_jobs(
    keywords: str | None = None,
    country: str | None = None,
    since: str | None = None,
    limit: int = 50,
) -> list[sqlite3.Row]:
    """Simple query helper for reviewing stored results."""
    clauses = []
    params: list = []

    if keywords:
        clauses.append("(title LIKE ? OR description LIKE ?)")
        params += [f"%{keywords}%", f"%{keywords}%"]
    if country:
        clauses.append("country = ?")
        params.append(country)
    if since:
        clauses.append("searched_at >= ?")
        params.append(since)

    where = f"WHERE {' AND '.join(clauses)}" if clauses else ""
    sql = f"SELECT * FROM job_listings {where} ORDER BY searched_at DESC, date_posted DESC LIMIT ?"
    params.append(limit)

    with _connect() as conn:
        return conn.execute(sql, params).fetchall()
