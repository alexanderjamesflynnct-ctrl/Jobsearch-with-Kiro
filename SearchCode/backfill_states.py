"""
Backfill missing US state abbreviations for existing job_listings rows
using Nominatim (OpenStreetMap) geocoding — no API key required.

Usage: py -3.14 SearchCode/backfill_states.py
       py -3.14 SearchCode/backfill_states.py --dry-run
"""

import argparse
import sqlite3
import time
from pathlib import Path

import requests

DB_PATH = Path(__file__).parent / "jobs.db"

US_STATES = {
    "alabama": "AL", "alaska": "AK", "arizona": "AZ", "arkansas": "AR",
    "california": "CA", "colorado": "CO", "connecticut": "CT", "delaware": "DE",
    "florida": "FL", "georgia": "GA", "hawaii": "HI", "idaho": "ID",
    "illinois": "IL", "indiana": "IN", "iowa": "IA", "kansas": "KS",
    "kentucky": "KY", "louisiana": "LA", "maine": "ME", "maryland": "MD",
    "massachusetts": "MA", "michigan": "MI", "minnesota": "MN", "mississippi": "MS",
    "missouri": "MO", "montana": "MT", "nebraska": "NE", "nevada": "NV",
    "new hampshire": "NH", "new jersey": "NJ", "new mexico": "NM", "new york": "NY",
    "north carolina": "NC", "north dakota": "ND", "ohio": "OH", "oklahoma": "OK",
    "oregon": "OR", "pennsylvania": "PA", "rhode island": "RI", "south carolina": "SC",
    "south dakota": "SD", "tennessee": "TN", "texas": "TX", "utah": "UT",
    "vermont": "VT", "virginia": "VA", "washington": "WA", "west virginia": "WV",
    "wisconsin": "WI", "wyoming": "WY", "district of columbia": "DC",
}


def geocode_state(location: str) -> str:
    try:
        resp = requests.get(
            "https://nominatim.openstreetmap.org/search",
            params={"q": location, "format": "json", "addressdetails": 1,
                    "limit": 1, "countrycodes": "us"},
            headers={"User-Agent": "JobSearchApp/1.0"},
            timeout=5,
        )
        data = resp.json()
        if data:
            state = data[0].get("address", {}).get("state", "")
            return US_STATES.get(state.lower().strip(), "")
    except Exception:
        pass
    return ""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row

    rows = conn.execute(
        "SELECT id, location FROM job_listings "
        "WHERE country = 'USA' AND (state IS NULL OR state = '') AND location IS NOT NULL"
    ).fetchall()

    if not rows:
        print("No US rows with missing state found.")
        conn.close()
        return

    print(f"Found {len(rows)} US row(s) with missing state. Geocoding...\n")
    updated = skipped = 0

    for i, row in enumerate(rows, 1):
        loc = row["location"]
        print(f"[{i}/{len(rows)}] {loc}", end=" → ")
        state = geocode_state(loc)
        if state:
            print(state)
            if not args.dry_run:
                conn.execute("UPDATE job_listings SET state = ? WHERE id = ?", (state, row["id"]))
                conn.commit()
            updated += 1
        else:
            print("(not found)")
            skipped += 1
        time.sleep(1)   # Nominatim: 1 req/sec

    conn.close()
    print(f"\nDone: {updated} updated, {skipped} not resolved")
    if args.dry_run:
        print("(dry-run — no changes written)")


if __name__ == "__main__":
    main()
