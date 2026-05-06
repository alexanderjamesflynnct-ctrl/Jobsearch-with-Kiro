"""
Re-fetches all LinkedIn job listings from the DB and updates is_remote
based on the guest API response.

Usage: py -3.14 SearchCode/update_remote_status.py
       py -3.14 SearchCode/update_remote_status.py --dry-run
"""

import argparse
import random
import sqlite3
import sys
import time
from pathlib import Path

import requests
from bs4 import BeautifulSoup

DB_PATH = Path(__file__).parent / "jobs.db"


def fetch_guest(job_id: str) -> str | None:
    url = f"https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{job_id}"
    try:
        r = requests.get(url, headers={
            "User-Agent": random.choice([
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15",
            ]),
            "Accept": "text/html,application/xhtml+xml",
        }, timeout=(10, 30))
        r.raise_for_status()
        return r.text
    except requests.RequestException as e:
        print(f"  Fetch failed: {e}")
        return None


def detect_remote(soup: BeautifulSoup, location: str) -> str:
    # Check job criteria
    for el in soup.select(".description__job-criteria-item"):
        label = el.select_one(".description__job-criteria-subheader")
        value = el.select_one(".description__job-criteria-text")
        if label and "workplace" in label.get_text(strip=True).lower():
            t = value.get_text(strip=True).lower() if value else ""
            if "remote"  in t: return "Remote"
            if "hybrid"  in t: return "Hybrid"
            if "on-site" in t or "onsite" in t: return "On-site"

    # Country-only location = remote on LinkedIn
    if location and "," not in location and location.strip() in (
        "United States", "Canada", "United Kingdom", "Australia",
        "Ireland", "New Zealand", "Singapore", "India", "Remote",
    ):
        return "Remote"

    # Scan description text
    text = soup.get_text().lower()
    if "fully remote" in text or "100% remote" in text: return "Remote"
    if "hybrid"  in text: return "Hybrid"
    if "remote"  in text: return "Remote"
    if "on-site" in text or "onsite" in text: return "On-site"
    return ""


def main():
    parser = argparse.ArgumentParser(description="Update is_remote for LinkedIn job listings")
    parser.add_argument("--dry-run", action="store_true", help="Show what would change without writing to DB")
    parser.add_argument("--id", dest="job_id", metavar="JOB_ID", help="Only process a single LinkedIn job ID")
    args = parser.parse_args()

    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row

    # Get LinkedIn rows — optionally filtered to a single job ID
    if args.job_id:
        rows = conn.execute(
            "SELECT id, url, location, is_remote FROM job_listings WHERE source = 'linkedin' AND url LIKE ?",
            (f"%{args.job_id}%",)
        ).fetchall()
    else:
        rows = conn.execute(
            "SELECT id, url, location, is_remote FROM job_listings WHERE source = 'linkedin'"
        ).fetchall()

    if not rows:
        print("No LinkedIn job listings found in DB.")
        conn.close()
        return

    print(f"Found {len(rows)} LinkedIn listing(s). Checking remote status...\n")

    updated = skipped = failed = 0

    for i, row in enumerate(rows, 1):
        job_id_match = __import__('re').search(r'/jobs/view/(\d+)', row['url'] or '')
        if not job_id_match:
            print(f"[{i}/{len(rows)}] Skipping — can't extract job ID from: {row['url']}")
            skipped += 1
            continue

        job_id = job_id_match.group(1)
        print(f"[{i}/{len(rows)}] {job_id} — current: {row['is_remote'] or '(none)'}", end="")

        html = fetch_guest(job_id)
        if not html:
            print(" — FETCH FAILED")
            failed += 1
            continue

        soup = BeautifulSoup(html, "lxml")
        new_remote = detect_remote(soup, row['location'] or "")

        if new_remote == (row['is_remote'] or ""):
            print(" — no change")
            skipped += 1
        else:
            print(f" → {new_remote or '(cleared)'}")
            if not args.dry_run:
                conn.execute(
                    "UPDATE job_listings SET is_remote = ? WHERE id = ?",
                    (new_remote or None, row['id'])
                )
                conn.commit()
            updated += 1

        if i < len(rows):
            time.sleep(random.uniform(1.0, 2.0))

    conn.close()

    print(f"\nDone: {updated} updated, {skipped} unchanged, {failed} failed")
    if args.dry_run:
        print("(dry-run — no changes written)")


if __name__ == "__main__":
    main()
