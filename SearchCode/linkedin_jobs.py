"""
LinkedIn Job Search Tool
Uses LinkedIn's public job search RSS feed to fetch listings.
No authentication required.
"""

import requests
import xml.etree.ElementTree as ET
from urllib.parse import urlencode
import argparse
from datetime import datetime


def search_linkedin_jobs(keywords: str, location: str = "", limit: int = 10) -> list[dict]:
    """
    Search LinkedIn job listings via their public RSS feed.

    Args:
        keywords: Job title or keywords to search for
        location: City, state, or country
        limit: Max number of results to return

    Returns:
        List of job dicts with title, company, location, date, and url
    """
    params = {
        "keywords": keywords,
        "location": location,
        "f_TPR": "r86400",  # posted in last 24 hours (optional)
        "position": 1,
        "pageNum": 0,
    }

    # LinkedIn public RSS endpoint
    base_url = "https://www.linkedin.com/jobs/search"
    rss_url = f"{base_url}?{urlencode(params)}&format=json"

    # Use the RSS feed URL format
    feed_url = f"https://www.linkedin.com/jobs/search/?{urlencode({'keywords': keywords, 'location': location})}"

    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
    }

    # Try the RSS feed
    rss_feed_url = (
        f"https://www.linkedin.com/jobs/search?keywords={requests.utils.quote(keywords)}"
        f"&location={requests.utils.quote(location)}&trk=public_jobs_jobs-search-bar_search-submit"
        f"&position=1&pageNum=0"
    )

    print(f"\nSearching LinkedIn for: '{keywords}' in '{location or 'Anywhere'}'\n")
    print(f"Full search URL: {feed_url}\n")

    try:
        response = requests.get(
            f"https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search"
            f"?keywords={requests.utils.quote(keywords)}&location={requests.utils.quote(location)}&start=0",
            headers=headers,
            timeout=10,
        )
        response.raise_for_status()

        # Parse the HTML response (LinkedIn returns HTML fragments for guest API)
        jobs = parse_job_cards(response.text, limit)
        return jobs

    except requests.RequestException as e:
        print(f"Request failed: {e}")
        print("\nTip: LinkedIn may block automated requests. Try the search URL directly in your browser:")
        print(f"  {feed_url}")
        return []


def parse_job_cards(html: str, limit: int) -> list[dict]:
    """Parse job cards from LinkedIn's guest API HTML response."""
    from html.parser import HTMLParser

    jobs = []

    class JobParser(HTMLParser):
        def __init__(self):
            super().__init__()
            self.current_job = {}
            self.capture = None
            self.depth = 0

        def handle_starttag(self, tag, attrs):
            attrs_dict = dict(attrs)
            classes = attrs_dict.get("class", "")

            if "base-card" in classes and "job" in classes.lower():
                self.current_job = {}

            if "base-search-card__title" in classes:
                self.capture = "title"
            elif "base-search-card__subtitle" in classes:
                self.capture = "company"
            elif "job-search-card__location" in classes:
                self.capture = "location"
            elif tag == "a" and "base-card__full-link" in classes:
                self.current_job["url"] = attrs_dict.get("href", "").split("?")[0]
            elif tag == "time":
                self.current_job["date"] = attrs_dict.get("datetime", "")

        def handle_data(self, data):
            if self.capture and data.strip():
                self.current_job[self.capture] = data.strip()
                if self.capture == "location" and self.current_job.get("title"):
                    jobs.append(dict(self.current_job))
                self.capture = None

    parser = JobParser()
    parser.feed(html)

    return jobs[:limit]


def display_jobs(jobs: list[dict]) -> None:
    """Pretty print job listings."""
    if not jobs:
        print("No jobs found. LinkedIn may be blocking the request.")
        print("Try searching directly at: https://www.linkedin.com/jobs/search/")
        return

    print(f"Found {len(jobs)} job(s):\n")
    print("-" * 60)

    for i, job in enumerate(jobs, 1):
        print(f"{i}. {job.get('title', 'N/A')}")
        print(f"   Company:  {job.get('company', 'N/A')}")
        print(f"   Location: {job.get('location', 'N/A')}")
        if job.get("date"):
            print(f"   Posted:   {job.get('date')}")
        if job.get("url"):
            print(f"   URL:      {job.get('url')}")
        print("-" * 60)


def main():
    parser = argparse.ArgumentParser(description="Search LinkedIn job listings")
    parser.add_argument("keywords", help="Job title or keywords (e.g. 'Python Developer')")
    parser.add_argument("--location", "-l", default="", help="Location (e.g. 'New York')")
    parser.add_argument("--limit", "-n", type=int, default=10, help="Number of results (default: 10)")

    args = parser.parse_args()

    jobs = search_linkedin_jobs(args.keywords, args.location, args.limit)
    display_jobs(jobs)


if __name__ == "__main__":
    main()
