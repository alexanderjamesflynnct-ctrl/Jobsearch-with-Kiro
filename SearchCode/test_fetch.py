import requests, sys, re
sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
from bs4 import BeautifulSoup

job_id = "4405146667"
r = requests.get(
    f"https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{job_id}",
    headers={"User-Agent": "Mozilla/5.0", "Accept": "text/html,application/xhtml+xml"},
)
soup = BeautifulSoup(r.text, "lxml")

# Try all company selectors
print("=== Company selectors ===")
for sel in [
    'a[href*="/company/"]',
    'a[data-tracking-control-name="public_jobs_topcard-org-name"]',
    '.topcard__org-name-link',
    '.topcard__flavor',
]:
    el = soup.select_one(sel)
    print(f"  {sel}: {el.get_text(strip=True)[:60] if el else 'NOT FOUND'}")

# Print all links to see what's there
print("\n=== All <a> tags with company-like hrefs ===")
for a in soup.find_all("a"):
    href = a.get("href", "")
    if "company" in href or "org" in href.lower():
        print(f"  href={href[:80]}  text={a.get_text(strip=True)[:40]}")
