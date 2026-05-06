import requests, sys
sys.path.insert(0, '.')
from bs4 import BeautifulSoup

job_id = "4385168689"
r = requests.get(
    f"https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{job_id}",
    headers={"User-Agent": "Mozilla/5.0", "Accept": "text/html,application/xhtml+xml"},
)
soup = BeautifulSoup(r.text, "lxml")

# Print every element that contains "remote" anywhere
print("=== Elements containing 'remote' ===")
for el in soup.find_all(True):
    if "remote" in el.get_text(strip=True).lower() and len(el.get_text(strip=True)) < 100:
        print(f"  tag={el.name} class={el.get('class')} text={repr(el.get_text(strip=True))}")
