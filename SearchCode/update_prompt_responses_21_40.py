"""Update prompts_log responses for sequences 21-40."""
import sqlite3
from pathlib import Path

DB_PATH = Path(__file__).parent / "jobs.db"
conn = sqlite3.connect(DB_PATH)

responses = {
    21: """Created delete_search.py script. Running with no args lists all searches with IDs and job counts. Pass an ID to delete that search + its jobs, or 'all' to wipe everything. Uses foreign key relationship to cascade deletes.""",

    22: """Created linkedin_import.py that accepts LinkedIn job URLs via CLI args or --file flag. Fetches public pages, parses title/company/location/type/description using BeautifulSoup, saves to jobs.db with deduplication. Adds small random delay between requests to be polite. Duplicate URLs silently skipped via INSERT OR IGNORE.""",

    23: """Added job_links table to the DB. Built C# endpoints: GET/POST/DELETE /links, POST /links/process. The process endpoint writes URLs to a temp file and calls linkedin_import.py. Built ImportLinks React page with URL input, pending queue table with timestamps, Run Import button, and processed section.""",

    24: """Updated C# URL validation to accept glassdoor.com and ziprecruiter.com. Updated React placeholder text and added green/orange badge colours. Added _parse_glassdoor and _parse_ziprecruiter functions to linkedin_import.py with appropriate selectors for each site.""",

    25: """The job_links table didn't exist in the database. Added CREATE TABLE IF NOT EXISTS for all tables (searches, job_listings, job_links) in the C# Program.cs startup block so they're created automatically on every boot - no migration needed.""",

    26: """Same root cause as #25. The C# startup now creates all tables on boot using CREATE TABLE IF NOT EXISTS, so this error can't recur.""",

    27: """Fixed React processLinks to properly reset processing state in finally block. Added POST /links/{id}/reset and POST /links/reset-all endpoints. Updated C# to only mark links as processed on successful Python exit (exit code 0). Added Reset All button and per-row reset button to UI.""",

    28: """beautifulsoup4 wasn't installed under Python 3.14. Provided: pip install beautifulsoup4 lxml. The C# API calls python which resolved to the old 3.10 install that didn't have the package.""",

    29: """Changed ProcessStartInfo.FileName from 'python' to 'py' with Arguments prefixed by '-3.14' since 'python' on the system PATH resolves to the old Python 3.10 install at C:\\Python310\\python.exe. The py launcher correctly routes to 3.14.""",

    30: """requests wasn't installed under Python 3.14 either. User ran: py -3.14 -m pip install requests beautifulsoup4 lxml""",

    31: """The checkmark character (U+2713) can't be encoded by Windows cp1252 console. Replaced with 'OK:' text. Also added sys.stdout.reconfigure(encoding='utf-8', errors='replace') at the top of the script to handle any unicode in job titles/companies.""",

    32: """Rewrote ImportLinks page with per-row progress tracking. Each link shows a spinning braille character animation while processing, turns green with 'Done' on success or red with 'Error' on failure. URLs display cleanly (hostname + path only). Added 'Back to Queue' button after processing completes.""",

    33: """Created delete_empty_jobs.py that removes job_listings where title, company, AND location are all empty/null. Also resets job_links back to pending so they can be re-imported once scraping is fixed.""",

    34: """Added DELETE /jobs/{id} endpoint to C# API. Added a delete button (X) column to JobTable component. Click deletes the job from the database and the table refreshes immediately.""",

    35: """LinkedIn returns a JavaScript shell from the main URL - the actual content is rendered client-side. Switched to the guest API endpoint: /jobs-guest/jobs/api/jobPosting/{id} which returns server-rendered HTML with real job data (title, company, location visible in the HTML).""",

    36: """Created test_fetch.py to verify the guest API response. Confirmed: title is in h2.top-card-layout__title, company in a[data-tracking-control-name='public_jobs_topcard-org-name'], location in .topcard__flavor elements. All three fields extracted correctly for the Pfizer test job.""",

    37: """Added source column to job_listings table and db.py. Created add_source_column.py migration that back-fills existing rows based on URL patterns (linkedin.com -> linkedin, indeed.com -> indeed, etc., default -> adzuna). Added Source column with colour-coded badges to the React table.""",

    38: """Added /sources, /job-types, /states API endpoints returning distinct values. Rebuilt Filters component with labelled dropdowns for Source, Country, State, Job Type, Sort By, Order, and Per Page. All filter immediately on change except keywords which requires Search button.""",

    39: """Added PATCH /jobs/{id}/country endpoint. Built CountryCell component that shows a dashed grey badge with pencil button for Unknown/LinkedIn countries. Clicking opens an inline dropdown with known countries - select and Save updates the DB immediately.""",

    40: """Created utils/timezone.js with country and US state to IANA timezone mapping. Added getTimezoneLabel() that calculates EST offset. Shows timezone info in the table for each job based on its country/state location.""",
}

count = 0
for seq, response in responses.items():
    conn.execute("UPDATE prompts_log SET response = ? WHERE sequence = ?", (response.strip(), seq))
    count += 1

conn.commit()
print(f"Updated {count} responses (sequences 21-40).")
conn.close()
