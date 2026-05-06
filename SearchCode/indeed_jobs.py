"""
Indeed Job Search Tool
Powered by python-jobspy — an actively maintained open-source library.

Install: pip install python-jobspy

Country config: edit countries.json to set which countries to search across.
Single country: use --country USA to override and search one country only.
"""

import argparse
import csv
import json
import sys
from pathlib import Path

COUNTRIES_FILE = Path(__file__).parent / "countries.json"


def load_countries() -> list[str]:
    """Load country list from countries.json, falling back to USA if missing."""
    if COUNTRIES_FILE.exists():
        data = json.loads(COUNTRIES_FILE.read_text(encoding="utf-8"))
        countries = data.get("countries", [])
        if countries:
            return countries
    return ["USA"]


def search_jobs(
    keywords: str,
    location: str = "",
    results: int = 20,
    hours_old: int = 72,
    job_type: str | None = None,
    is_remote: bool = False,
    country: str | None = None,
) -> list[dict]:
    """
    Search Indeed jobs for one or more countries.
    If country is provided, searches only that country.
    Otherwise, searches all countries listed in countries.json.
    """
    try:
        from jobspy import scrape_jobs
    except ImportError:
        print("Missing dependency. Run: pip install python-jobspy")
        sys.exit(1)

    countries = [country] if country else load_countries()

    all_jobs: list[dict] = []
    seen_urls: set[str] = set()

    for c in countries:
        print(f"\nSearching Indeed [{c}] for: '{keywords}'"
              f"{f' in {location}' if location else ''}"
              f"{' (Remote)' if is_remote else ''}")

        try:
            df = scrape_jobs(
                site_name=["indeed"],
                search_term=keywords,
                location=location or None,
                results_wanted=results,
                hours_old=hours_old,
                job_type=job_type,
                is_remote=is_remote or None,
                country_indeed=c,
            )
        except Exception as e:
            print(f"  Warning: search failed for {c} — {e}")
            continue

        if df is None or df.empty:
            print(f"  No results found for {c}.")
            continue

        for _, row in df.iterrows():
            url = row.get("job_url", "")
            if url in seen_urls:
                continue  # skip duplicates across countries
            seen_urls.add(url)

            all_jobs.append({
                "country":     c,
                "title":       row.get("title", ""),
                "company":     row.get("company", ""),
                "location":    f"{row.get('city', '')} {row.get('state', '')}".strip(),
                "job_type":    row.get("job_type", ""),
                "salary":      _format_salary(row),
                "posted":      str(row.get("date_posted", "")),
                "url":         url,
                "description": str(row.get("description", ""))[:300] if row.get("description") else "",
            })

        print(f"  Found {len(df)} result(s).")

    return all_jobs


def _format_salary(row) -> str:
    lo = row.get("min_amount")
    hi = row.get("max_amount")
    interval = row.get("interval", "")
    if lo and hi:
        return f"${lo:,.0f} - ${hi:,.0f} / {interval}"
    if lo:
        return f"${lo:,.0f}+ / {interval}"
    return ""


def display_jobs(jobs: list[dict]) -> None:
    if not jobs:
        print("\nNo jobs found. Try broader keywords or a larger --hours value.")
        return

    print(f"\nTotal: {len(jobs)} job(s) found\n")
    sep = "-" * 65

    for i, job in enumerate(jobs, 1):
        print(sep)
        print(f"{i}. {job['title']}  [{job['country']}]")
        print(f"   Company:  {job['company']}")
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
        description="Search Indeed job listings across one or multiple countries",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Search all countries in countries.json
  python indeed_jobs.py "Python Developer"

  # Search a single country only
  python indeed_jobs.py "Data Scientist" --country UK

  # Filter by location within country search
  python indeed_jobs.py "DevOps Engineer" --location "London" --country UK

  # Remote jobs across all configured countries
  python indeed_jobs.py "ML Engineer" --remote --results 10

  # Save multi-country results to CSV
  python indeed_jobs.py "Frontend Developer" --type fulltime --save jobs.csv
        """,
    )
    parser.add_argument("keywords", help="Job title or keywords")
    parser.add_argument("--location", "-l", default="", help="City or region to filter within each country")
    parser.add_argument("--results", "-n", type=int, default=20, help="Results per country (default: 20)")
    parser.add_argument("--hours", type=int, default=72, help="Max age of posting in hours (default: 72)")
    parser.add_argument(
        "--type", "-t", dest="job_type",
        choices=["fulltime", "parttime", "internship", "contract"],
        help="Filter by job type",
    )
    parser.add_argument("--remote", "-r", action="store_true", help="Remote jobs only")
    parser.add_argument(
        "--country", "-c", default=None,
        help="Search a single country only (overrides countries.json). E.g. USA, UK, Canada",
    )
    parser.add_argument("--save", "-s", metavar="FILE.csv", help="Save results to a CSV file")

    args = parser.parse_args()

    jobs = search_jobs(
        keywords=args.keywords,
        location=args.location,
        results=args.results,
        hours_old=args.hours,
        job_type=args.job_type,
        is_remote=args.remote,
        country=args.country,
    )

    display_jobs(jobs)

    if args.save:
        save_csv(jobs, args.save)


if __name__ == "__main__":
    main()
