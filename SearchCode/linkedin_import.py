"""
LinkedIn Job Importer
Accepts LinkedIn job URLs, scrapes the public page, and saves to jobs.db.

Install: pip install requests beautifulsoup4 lxml

Usage:
  # Single URL
  python SearchCode/linkedin_import.py "https://www.linkedin.com/jobs/view/1234567890"

  # Multiple URLs
  python SearchCode/linkedin_import.py url1 url2 url3

  # From a text file (one URL per line)
  python SearchCode/linkedin_import.py --file urls.txt
"""

import argparse
import hashlib
import random
import re
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

# Force UTF-8 output to avoid cp1252 encoding errors on Windows
if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

import requests
from bs4 import BeautifulSoup

sys.path.insert(0, str(Path(__file__).parent))
from db import init_db, save_search, save_jobs

# ── HTTP ─────────────────────────────────────────────────────────────────────

USER_AGENTS = [
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15",
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
]


def fetch(url: str) -> str | None:
    """Fetch a job page using the appropriate endpoint."""
    # Extract job ID and use the guest API for LinkedIn (returns real HTML, not JS shell)
    if 'linkedin.com' in url:
        m = re.search(r'/jobs/view/(?:[^/?]+-)?(\d+)', url)
        if m:
            api_url = f"https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{m.group(1)}"
            try:
                resp = requests.get(api_url, headers={
                    "User-Agent": random.choice(USER_AGENTS),
                    "Accept": "text/html,application/xhtml+xml",
                }, timeout=(10, 30))
                resp.raise_for_status()
                return resp.text
            except requests.RequestException as e:
                print(f"  Fetch failed: {e}")
                return None

    # For Indeed, Glassdoor, ZipRecruiter — fetch the page directly
    clean = url.split('?')[0]
    headers = {
        "User-Agent": random.choice(USER_AGENTS),
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "en-US,en;q=0.9",
    }
    try:
        resp = requests.get(clean, headers=headers, timeout=(10, 30))
        resp.raise_for_status()
        return resp.text
    except requests.RequestException as e:
        print(f"  Fetch failed: {e}")
        return None


# ── PARSING ──────────────────────────────────────────────────────────────────

def _text(el) -> str:
    return re.sub(r"\s+", " ", el.get_text(" ", strip=True)).strip() if el else ""


def extract_job_id(url: str) -> str:
    """Extract a stable job ID from the URL, or hash it as fallback."""
    # LinkedIn: /jobs/view/TITLE-12345 or /jobs/view/12345
    m = re.search(r"/jobs/view/(?:[^/]+-)?(\d+)", url)
    if m:
        return f"li_{m.group(1)}"
    # Indeed: jk=abc123
    m = re.search(r"jk=([a-zA-Z0-9]+)", url)
    if m:
        return f"in_{m.group(1)}"
    # Glassdoor: /job-listing/...-JOBID
    m = re.search(r"jobListingId=(\d+)", url)
    if m:
        return f"gd_{m.group(1)}"
    # ZipRecruiter: /jobs/... numeric id
    m = re.search(r"/jobs/[^/]+-(\w{8,})", url)
    if m:
        return f"zr_{m.group(1)}"
    return f"imp_{hashlib.md5(url.encode()).hexdigest()[:12]}"


def parse_job(html: str, url: str) -> dict:
    soup = BeautifulSoup(html, "lxml")

    if "glassdoor.com" in url:
        return _parse_glassdoor(soup, url)
    if "ziprecruiter.com" in url:
        return _parse_ziprecruiter(soup, url)
    if "indeed.com" in url:
        return _parse_indeed(soup, url)
    return _parse_linkedin(soup, url)


def _base_job(url: str) -> dict:
    source = ("linkedin"     if "linkedin.com"     in url else
              "indeed"       if "indeed.com"       in url else
              "glassdoor"    if "glassdoor.com"    in url else
              "ziprecruiter" if "ziprecruiter.com" in url else "unknown")
    return {
        "adzuna_id":   extract_job_id(url),
        "country":     "Unknown",
        "title":       "",
        "company":     "",
        "location":    "",
        "city":        "",
        "state":       "",
        "job_type":    "",
        "salary":      "",
        "posted":      "",
        "url":         url,
        "source":      source,
        "description": "",
    }


def _infer_country(location: str) -> str:
    t = location.lower()
    if any(x in t for x in (" ca", " ny", " tx", " wa", " il", " fl", "united states")):
        return "USA"
    if any(x in t for x in ("london", "manchester", "birmingham", "united kingdom")):
        return "UK"
    if any(x in t for x in ("toronto", "vancouver", "ontario", "british columbia", "canada")):
        return "Canada"
    if any(x in t for x in ("sydney", "melbourne", "brisbane", "australia")):
        return "Australia"
    if any(x in t for x in ("dublin", "ireland")):
        return "Ireland"
    if any(x in t for x in ("auckland", "wellington", "new zealand")):
        return "New Zealand"
    return "Unknown"


def _state_from_location(location: str) -> str:
    m = re.search(r",\s*([A-Z]{2})\b", location)
    return m.group(1) if m else ""


def _detect_remote(soup: BeautifulSoup, location: str = "") -> str:
    """Detect workplace type from job criteria, description text, or location pattern."""
    # Check job criteria items first (most reliable)
    for el in soup.select(".description__job-criteria-item"):
        label = el.select_one(".description__job-criteria-subheader")
        value = el.select_one(".description__job-criteria-text")
        if label and "workplace" in label.get_text(strip=True).lower():
            t = value.get_text(strip=True).lower() if value else ""
            if "remote"  in t: return "Remote"
            if "hybrid"  in t: return "Hybrid"
            if "on-site" in t or "onsite" in t: return "On-site"

    # LinkedIn remote jobs show country-only location (no comma = no city/state)
    if location and "," not in location and location.strip() in (
        "United States", "Canada", "United Kingdom", "Australia",
        "Ireland", "New Zealand", "Singapore", "India", "Remote",
    ):
        return "Remote"

    # Fall back to scanning description text
    text = soup.get_text().lower()
    if "fully remote" in text or "100% remote" in text:
        return "Remote"
    if "hybrid" in text:
        return "Hybrid"
    if "remote" in text:
        return "Remote"
    if "on-site" in text or "onsite" in text or "in office" in text:
        return "On-site"
    return ""


def _parse_linkedin(soup: BeautifulSoup, url: str) -> dict:
    job = _base_job(url)

    # Guest API selectors (from /jobs-guest/jobs/api/jobPosting/{id})
    job["title"] = _text(
        soup.select_one("h2.top-card-layout__title")
        or soup.select_one("h1")
    )

    job["company"] = _text(
        soup.select_one("a[data-tracking-control-name='public_jobs_topcard-org-name']")
        or soup.select_one(".topcard__org-name-link")
    )
    # Fallback: find any /company/ link with actual text
    if not job["company"]:
        for a in soup.select('a[href*="/company/"]'):
            t = _text(a)
            if t:
                job["company"] = t
                break

    loc_el = (
        soup.select_one(".topcard__flavor--bullet")
        or soup.select_one('[class*="topcard__flavor"]')
    )
    # Location is often the second span in the flavor row
    if not loc_el:
        flavors = soup.select(".topcard__flavor")
        loc_el = flavors[1] if len(flavors) > 1 else (flavors[0] if flavors else None)
    job["location"] = _text(loc_el)

    job["state"]   = _state_from_location(job["location"])
    job["city"]    = job["location"].split(",")[0].strip() if "," in job["location"] else job["location"]
    job["country"] = _infer_country(job["location"]) if job["location"] else "LinkedIn"

    time_el = soup.select_one("time")
    if time_el:
        job["posted"] = time_el.get("datetime", _text(time_el))[:10]
    else:
        # Guest API uses a span with relative text like "2 weeks ago"
        posted_el = (
            soup.select_one(".posted-time-ago__text")
            or soup.select_one('[class*="posted-time"]')
            or soup.select_one('[class*="listed-time"]')
        )
        job["posted"] = _text(posted_el) if posted_el else ""

    # Job criteria (type, seniority, etc.)
    for el in soup.select(".description__job-criteria-text"):
        t = _text(el).lower()
        if any(x in t for x in ("full-time", "part-time", "contract", "internship")):
            job["job_type"] = _text(el)
            break

    desc_el = (
        soup.select_one(".description__text")
        or soup.select_one('[class*="show-more-less-html"]')
    )
    job["description"] = _text(desc_el)[:300] if desc_el else ""
    job["is_remote"]   = _detect_remote(soup, job["location"])

    return job


def _parse_indeed(soup: BeautifulSoup, url: str) -> dict:
    job = _base_job(url)

    job["title"]   = _text(soup.select_one("h1"))
    job["company"] = _text(
        soup.select_one('[data-company-name]')
        or soup.select_one('[class*="companyName"]')
    )
    loc_el = soup.select_one('[data-testid="job-location"]') or soup.select_one('[class*="companyLocation"]')
    job["location"] = _text(loc_el)
    job["state"]    = _state_from_location(job["location"])
    job["city"]     = job["location"].split(",")[0].strip() if "," in job["location"] else job["location"]
    job["country"]  = _infer_country(job["location"]) if job["location"] else "Indeed"

    salary_el = soup.select_one('[id*="salaryInfoAndJobType"]') or soup.select_one('[class*="salary"]')
    job["salary"] = _text(salary_el)

    desc_el = soup.select_one('[id="jobDescriptionText"]') or soup.select_one('[class*="jobDescription"]')
    job["description"] = _text(desc_el)[:300] if desc_el else ""
    job["is_remote"]   = _detect_remote(soup)
    return job


def _parse_glassdoor(soup: BeautifulSoup, url: str) -> dict:
    job = _base_job(url)

    job["title"]   = _text(soup.select_one("h1"))
    job["company"] = _text(
        soup.select_one('[class*="employerName"]')
        or soup.select_one('[data-test="employer-name"]')
    )
    loc_el = soup.select_one('[data-test="location"]') or soup.select_one('[class*="location"]')
    job["location"] = _text(loc_el)
    job["state"]    = _state_from_location(job["location"])
    job["city"]     = job["location"].split(",")[0].strip() if "," in job["location"] else job["location"]
    job["country"]  = _infer_country(job["location"]) if job["location"] else "Glassdoor"

    salary_el = soup.select_one('[data-test="detailSalary"]') or soup.select_one('[class*="salary"]')
    job["salary"] = _text(salary_el)

    desc_el = soup.select_one('[class*="jobDescriptionContent"]') or soup.select_one('[class*="desc"]')
    job["description"] = _text(desc_el)[:300] if desc_el else ""
    job["is_remote"]   = _detect_remote(soup)
    return job


def _parse_ziprecruiter(soup: BeautifulSoup, url: str) -> dict:
    job = _base_job(url)

    job["title"]   = _text(soup.select_one("h1"))
    job["company"] = _text(
        soup.select_one('[class*="hiring_company_name"]')
        or soup.select_one('[class*="company"]')
    )
    loc_el = soup.select_one('[class*="location"]') or soup.select_one('[data-name="location"]')
    job["location"] = _text(loc_el)
    job["state"]    = _state_from_location(job["location"])
    job["city"]     = job["location"].split(",")[0].strip() if "," in job["location"] else job["location"]
    job["country"]  = _infer_country(job["location"]) if job["location"] else "USA"

    salary_el = soup.select_one('[class*="salary"]') or soup.select_one('[data-name="salary"]')
    job["salary"] = _text(salary_el)

    desc_el = soup.select_one('[class*="jobDescriptionSection"]') or soup.select_one('[class*="description"]')
    job["description"] = _text(desc_el)[:300] if desc_el else ""
    job["is_remote"]   = _detect_remote(soup)
    return job


# ── MAIN ─────────────────────────────────────────────────────────────────────

def process_urls(urls: list[str]) -> None:
    init_db()
    search_id, searched_at = save_search("LinkedIn Import", "", "LinkedIn")

    jobs = []
    for i, url in enumerate(urls, 1):
        url = url.strip()
        if not url:
            continue

        # Clean URL — strip tracking params, use canonical LinkedIn job ID URL
        clean_url = url.split('?')[0]
        m = re.search(r'/jobs/view/(?:[^/?]+-)?(\d+)', url)
        if m and 'linkedin.com' in url:
            clean_url = f"https://www.linkedin.com/jobs/view/{m.group(1)}/"

        print(f"[{i}/{len(urls)}] Fetching: {clean_url}")
        html = fetch(url)
        if not html:
            print(f"  Skipped.")
            continue

        job = parse_job(html, clean_url)
        jobs.append(job)
        title   = job['title']   or '(no title)'
        company = job['company'] or '(no company)'
        loc     = job['location'] or '(no location)'
        print(f"  OK: {title} | {company} | {loc}")

        if i < len(urls):
            time.sleep(random.uniform(1.5, 3.0))

    if jobs:
        inserted, skipped = save_jobs(jobs, search_id, searched_at)
        print(f"\nDone: {inserted} job(s) saved, {skipped} duplicate(s) skipped")
    else:
        print("\nNo jobs were saved.")


def main():
    parser = argparse.ArgumentParser(
        description="Import LinkedIn job URLs into jobs.db",
        epilog="""
Examples:
  python SearchCode/linkedin_import.py "https://www.linkedin.com/jobs/view/1234567890"
  python SearchCode/linkedin_import.py url1 url2 url3
  python SearchCode/linkedin_import.py --file my_urls.txt
        """,
    )
    parser.add_argument("urls", nargs="*", help="One or more LinkedIn job URLs")
    parser.add_argument("--file", "-f", metavar="FILE", help="Text file with one URL per line")

    args = parser.parse_args()

    urls = list(args.urls)

    if args.file:
        path = Path(args.file)
        if not path.exists():
            print(f"File not found: {args.file}")
            sys.exit(1)
        urls += [line.strip() for line in path.read_text().splitlines() if line.strip()]

    if not urls:
        parser.print_help()
        sys.exit(1)

    # Validate they look like supported job URLs
    valid = []
    supported = ("linkedin.com/jobs", "indeed.com", "glassdoor.com", "ziprecruiter.com")
    for u in urls:
        if any(s in u for s in supported):
            valid.append(u)
        else:
            print(f"Skipping (unsupported URL): {u}")

    if not valid:
        print("No valid LinkedIn job URLs provided.")
        sys.exit(1)

    process_urls(valid)


if __name__ == "__main__":
    main()
