"""
Adzuna Job Search Tool
Official licensed API — free tier: 250 requests/day

Sign up at: https://developer.adzuna.com/signup
Then create a .env file with your ADZUNA_APP_ID and ADZUNA_APP_KEY
  (copy .env.example as a starting point)

Install: pip install requests python-dotenv
"""

import argparse
import csv
import json
import os
import sys
import time
from pathlib import Path

import requests
from db import init_db, save_search, save_jobs

# ---------------------------------------------------------------------------
# Country name → Adzuna 2-letter code mapping
# Covers all countries in countries.json plus extras for --country overrides
# ---------------------------------------------------------------------------
COUNTRY_CODES: dict[str, str] = {
    "usa":         "us",
    "uk":          "gb",
    "canada":      "ca",
    "australia":   "au",
    "ireland":     "ie",
    "new zealand": "nz",
    "singapore":   "sg",
    "south africa":"za",
    "india":       "in",
    "germany":     "de",
    "france":      "fr",
    "netherlands": "nl",
    "brazil":      "br",
    "mexico":      "mx",
    "poland":      "pl",
    "russia":      "ru",
}

COUNTRIES_FILE = Path(__file__).parent / "countries.json"
API_BASE = "https://api.adzuna.com/v1/api/jobs"

# ── US State lookup ──────────────────────────────────────────────────────────

# Full state name → abbreviation
US_STATES: dict[str, str] = {
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

# Cache geocode results to avoid duplicate API calls within a run
_geocode_cache: dict[str, str] = {}


def _state_abbr(name: str) -> str:
    """Convert a full state name to its 2-letter abbreviation."""
    return US_STATES.get(name.lower().strip(), "")


def _extract_state_from_area(area: list[str], country: str) -> str:
    """
    Parse Adzuna's area array for US jobs.
    Adzuna area structures vary:
      ["USA", "South East", "Georgia", "Atlanta"]          → area[2] = state
      ["USA", "Georgia", "Atlanta, Fulton County"]         → area[1] = state
      ["USA", "New York", "New York City", "Manhattan"]    → area[1] = state
    Strategy: scan all area elements for a known US state name.
    """
    if country != "USA" or not area:
        return ""
    for part in area[1:]:          # skip "USA" at index 0
        clean = part.split(",")[0].strip()
        abbr = _state_abbr(clean)
        if abbr:
            return abbr
    return ""


def _geocode_state(location_display: str) -> str:
    """
    Use Nominatim (OpenStreetMap) to resolve a location string to a US state.
    Results are cached. Returns 2-letter state abbreviation or "".
    """
    if not location_display:
        return ""

    key = location_display.lower().strip()
    if key in _geocode_cache:
        return _geocode_cache[key]

    try:
        resp = requests.get(
            "https://nominatim.openstreetmap.org/search",
            params={
                "q":              location_display,
                "format":         "json",
                "addressdetails": 1,
                "limit":          1,
                "countrycodes":   "us",
            },
            headers={"User-Agent": "JobSearchApp/1.0"},
            timeout=5,
        )
        data = resp.json()
        if data:
            addr  = data[0].get("address", {})
            state = addr.get("state", "")
            abbr  = _state_abbr(state)
            _geocode_cache[key] = abbr
            time.sleep(1)   # Nominatim rate limit: 1 req/sec
            return abbr
    except Exception:
        pass

    _geocode_cache[key] = ""
    return ""


def load_credentials() -> tuple[str, str]:
    """Load API credentials from .env file or environment variables."""
    env_file = Path(__file__).parent / ".env"
    if env_file.exists():
        # Simple .env parser (avoids requiring python-dotenv)
        for line in env_file.read_text().splitlines():
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                key, _, value = line.partition("=")
                os.environ.setdefault(key.strip(), value.strip())

    app_id  = os.environ.get("ADZUNA_APP_ID", "")
    app_key = os.environ.get("ADZUNA_APP_KEY", "")

    if not app_id or not app_key:
        print("Error: Adzuna credentials not found.")
        print("  1. Sign up free at https://developer.adzuna.com/signup")
        print("  2. Copy .env.example to .env and add your APP_ID and APP_KEY")
        sys.exit(1)

    return app_id, app_key


def load_countries() -> list[str]:
    """Load country list from countries.json."""
    if COUNTRIES_FILE.exists():
        data = json.loads(COUNTRIES_FILE.read_text(encoding="utf-8"))
        countries = data.get("countries", [])
        if countries:
            return countries
    return ["USA"]


def resolve_country_code(country_name: str) -> str | None:
    """Convert a country name to its Adzuna API code."""
    code = COUNTRY_CODES.get(country_name.lower().strip())
    if not code:
        print(f"  Warning: '{country_name}' is not a recognised Adzuna country — skipping.")
    return code


def search_country(
    app_id: str,
    app_key: str,
    country_code: str,
    keywords: str,
    location: str,
    results: int,
    job_type: str | None,
    salary_min: int | None,
    sort_by: str,
    page: int = 1,
) -> list[dict]:
    """Call the Adzuna search endpoint for a single country."""
    params: dict = {
        "app_id":           app_id,
        "app_key":          app_key,
        "what":             keywords,
        "results_per_page": min(results, 50),   # Adzuna max per page is 50
        "sort_by":          sort_by,
        "content-type":     "application/json",
    }

    if location:
        params["where"] = location
    if salary_min:
        params["salary_min"] = salary_min
    if job_type == "fulltime":
        params["full_time"] = 1
    elif job_type == "parttime":
        params["part_time"] = 1
    elif job_type == "contract":
        params["contract"] = 1
    elif job_type == "permanent":
        params["permanent"] = 1

    url = f"{API_BASE}/{country_code}/search/{page}"

    try:
        response = requests.get(url, params=params, timeout=10)
        response.raise_for_status()
        data = response.json()
        return data.get("results", [])
    except requests.HTTPError as e:
        print(f"  HTTP error for {country_code.upper()}: {e}")
        return []
    except requests.RequestException as e:
        print(f"  Request failed for {country_code.upper()}: {e}")
        return []


def search_jobs(
    keywords: str,
    location: str = "",
    results: int = 20,
    job_type: str | None = None,
    salary_min: int | None = None,
    sort_by: str = "date",
    country: str | None = None,
) -> list[dict]:
    app_id, app_key = load_credentials()
    countries = [country] if country else load_countries()

    all_jobs: list[dict] = []
    seen_ids: set[str] = set()

    for c in countries:
        code = resolve_country_code(c)
        if not code:
            continue

        print(f"  Searching Adzuna [{c}]...")

        raw = search_country(
            app_id, app_key, code,
            keywords, location, results,
            job_type, salary_min, sort_by,
        )

        count = 0
        for job in raw:
            job_id = str(job.get("id", ""))
            if job_id in seen_ids:
                continue
            seen_ids.add(job_id)

            salary_min_val = job.get("salary_min")
            salary_max_val = job.get("salary_max")
            salary_str = ""
            if salary_min_val and salary_max_val:
                salary_str = f"${salary_min_val:,.0f} - ${salary_max_val:,.0f}"
            elif salary_min_val:
                salary_str = f"${salary_min_val:,.0f}+"

            loc          = job.get("location", {})
            area         = loc.get("area", [])
            display_name = loc.get("display_name", "")
            city         = area[-1] if len(area) > 1 else display_name

            # Resolve US state: try area array first, fall back to Nominatim geocoding
            if c == "USA":
                state = _extract_state_from_area(area, c)
                if not state and display_name:
                    state = _geocode_state(display_name)
            else:
                state = ""
            all_jobs.append({
                "adzuna_id":   job_id,
                "country":     c,
                "title":       job.get("title", ""),
                "company":     job.get("company", {}).get("display_name", ""),
                "location":    loc.get("display_name", ""),
                "city":        city,
                "state":       state,                "job_type":    job.get("contract_time", "") or job.get("contract_type", ""),
                "salary":      salary_str,
                "posted":      job.get("created", "")[:10],
                "url":         job.get("redirect_url", ""),
                "source":      "adzuna",
                "description": job.get("description", "")[:300],
            })
            count += 1

        print(f"    Found {count} result(s).")

    return all_jobs


def display_jobs(jobs: list[dict]) -> None:
    if not jobs:
        print("\nNo jobs found. Try broader keywords or remove salary/type filters.")
        return

    print(f"\nTotal: {len(jobs)} job(s) found\n")
    sep = "-" * 65

    for i, job in enumerate(jobs, 1):
        print(sep)
        print(f"{i}. {job['title']}  [{job['country']}]")
        print(f"   Company:  {job['company'] or 'N/A'}")
        print(f"   Location: {job['location'] or 'N/A'}")
        if job["job_type"]:
            print(f"   Type:     {job['job_type']}")
        if job["salary"]:
            print(f"   Salary:   {job['salary']}")
        if job["posted"]:
            print(f"   Posted:   {job['posted']}")
        if job["description"]:
            print(f"   Preview:  {job['description']}...")
        print(f"   URL:      {job['url']}")

    print(sep)


def save_csv(jobs: list[dict], filename: str) -> None:
    if not jobs:
        return
    with open(filename, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=jobs[0].keys())
        writer.writeheader()
        writer.writerows(jobs)
    print(f"\nSaved {len(jobs)} jobs to {filename}")


def main():
    parser = argparse.ArgumentParser(
        description="Search job listings via the Adzuna API (licensed, free tier)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Setup:
  1. Sign up at https://developer.adzuna.com/signup
  2. Copy .env.example to .env and add your credentials

Examples:
  python adzuna_jobs.py "Director of Software Engineering"
  python adzuna_jobs.py "Data Scientist" --location "London"
  python adzuna_jobs.py "DevOps Engineer" --country UK --results 50
  python adzuna_jobs.py "ML Engineer" --type permanent --salary-min 80000
  python adzuna_jobs.py "Frontend Developer" --sort salary --save jobs.csv
        """,
    )
    parser.add_argument("keywords", help="Job title or keywords")
    parser.add_argument("--location", "-l", default="", help="City or region to search within")
    parser.add_argument("--results", "-n", type=int, default=20, help="Results per country, max 50 (default: 20)")
    parser.add_argument(
        "--type", "-t", dest="job_type",
        choices=["fulltime", "parttime", "contract", "permanent"],
        help="Filter by contract type",
    )
    parser.add_argument("--salary-min", type=int, metavar="AMOUNT", help="Minimum salary filter")
    parser.add_argument(
        "--sort", default="date",
        choices=["date", "salary", "relevance"],
        help="Sort results by (default: date)",
    )
    parser.add_argument(
        "--country", "-c", default=None,
        help="Search a single country only, overrides countries.json (e.g. USA, UK, Canada)",
    )
    parser.add_argument("--save", "-s", metavar="FILE.csv", help="Save results to CSV")

    args = parser.parse_args()

    print(f"\nSearching Adzuna for: '{args.keywords}'"
          f"{f' in {args.location}' if args.location else ''}\n")

    init_db()

    jobs = search_jobs(
        keywords=args.keywords,
        location=args.location,
        results=args.results,
        job_type=args.job_type,
        salary_min=args.salary_min,
        sort_by=args.sort,
        country=args.country,
    )

    display_jobs(jobs)

    if jobs:
        search_id, searched_at = save_search(args.keywords, args.location, args.country)
        inserted, skipped = save_jobs(jobs, search_id, searched_at)
        print(f"\nDatabase: {inserted} new job(s) saved, {skipped} duplicate(s) skipped  →  jobs.db")

    if args.save:
        save_csv(jobs, args.save)


if __name__ == "__main__":
    main()
