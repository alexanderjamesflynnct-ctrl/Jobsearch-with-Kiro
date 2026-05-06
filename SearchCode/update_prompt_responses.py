"""
Update prompts_log with fuller verbatim responses.
Run after import_prompts_to_db.py
"""
import sqlite3
from pathlib import Path

DB_PATH = Path(__file__).parent / "jobs.db"
conn = sqlite3.connect(DB_PATH)

responses = {
    1: """I can help you build a job search tool, but there's an important caveat: LinkedIn doesn't have a public job search API, and scraping their site violates their Terms of Service. That said, there are a couple of legitimate approaches:

1. LinkedIn Jobs RSS feed - LinkedIn exposes job listings via RSS (no auth required)
2. LinkedIn API - requires a partner account, very restricted
3. Web scraping - works technically but risks account bans and ToS violations

The most practical and ToS-friendly option is using the RSS feed approach combined with their public job search URL. I built linkedin_jobs.py using their guest jobs API which is the most permissive public endpoint they expose. If it gets blocked, the script prints the direct search URL you can open in a browser.""",

    2: """python-jobspy is the way to go here - it's actively maintained, handles Indeed (and LinkedIn, Glassdoor, etc.) with a clean API, and returns structured data. Indeed's official API is partner-only and their old public API was deprecated. I built a solid wrapper around JobSpy with CLI support for keywords, location, results count, hours old, job type, remote filter, country, and CSV export.""",

    3: """Both files are now in the SearchCode/ folder. Run them from there:
python SearchCode/indeed_jobs.py "Software Engineer"
python SearchCode/linkedin_jobs.py "Data Scientist" """,

    4: """That's outside what I can do - upgrading Python requires downloading and installing a new version on your system, which isn't something I can execute safely on your behalf. I provided instructions for the official installer, winget, and pyenv-win approaches.""",

    5: """Both files will run fine on Python 3.14. The only Python 3.14 removals that could affect code are things like ast.Num/Str, asyncio child watchers, and some argparse/sqlite3 internals - none of which are used here. One minor update: swapped Optional[str] to str | None which is the modern syntax preferred since 3.10.""",

    6: """Both options make sense - I implemented both: a countries.json config file for multi-country searches, while keeping single --country as a quick override. The script searches each country sequentially, aggregates results, deduplicates jobs by URL, shows a [COUNTRY] tag on each result, and --results is per-country.""",

    7: """Updated countries.json with all predominantly English-speaking countries supported by JobSpy: USA, UK, Canada, Australia, Ireland, New Zealand, Singapore, South Africa, Nigeria, Philippines, India, Pakistan, Hong Kong. All use the exact spelling JobSpy requires.""",

    8: """python SearchCode/indeed_jobs.py "Director of Software Engineering"

That'll search across all 13 countries in your countries.json. Variations provided for limiting results, full-time only, single country, remote only, and saving to CSV.""",

    9: """Short answer: not really, for job seekers. Indeed's old Publisher API was deprecated and shut down. The current Partner API is GraphQL-based and gated behind an approved partnership. Recommended Adzuna API as the best alternative - it aggregates from Indeed and other boards, has a real free tier (250 req/day), covers most countries, and gives you a proper API key with no scraping involved.""",

    10: """Built adzuna_jobs.py with: .env credentials management, country code mapping (name to 2-letter ISO), multi-country search from countries.json, single country override, up to 50 results per country per call, salary/job type/sort filters, CSV export, and proper error handling. The free tier gives 250 requests/day - across 6 countries that's ~40 full searches per day.""",

    11: """Created db.py with two tables: searches (one row per search run with searched_at, keywords, location, country) and job_listings (one row per job with Adzuna's stable job ID as PK, search_id FK, searched_at, date_posted, and all job fields). INSERT OR IGNORE on the job ID means re-running the same search won't create duplicates. jobs.db is created automatically in SearchCode/ on first run.""",

    12: """Provided winget install SQLite.SQLite and choco install sqlite commands. Also showed a Python one-liner alternative that works without the CLI installed: python -c "import sqlite3; ..." """,

    13: """Found it at C:\\Users\\lex\\AppData\\Local\\Microsoft\\WinGet\\Packages\\SQLite.SQLite_Microsoft.Winget.Source_8wekyb3d8bbwe - that path is in your system PATH but the Kiro terminal isn't picking it up.""",

    14: """Provided instructions to: find the profile path with echo $PROFILE, create it if needed with New-Item, open with notepad $PROFILE, add the PATH line, save, and reload with . $PROFILE.""",

    15: """Provided multiple examples: Python one-liners for viewing latest jobs, counting by country, listing searches. Also showed how to use the db.py query_jobs() function directly with keyword/country/since filters.""",

    16: """Created a FastAPI backend (webapp/api/) serving the DB data and a React frontend (webapp/client/) with Vite. Built JobTable, Filters, and StatsBar components. The UI has stats bar, keyword search, country/sort/order/per-page filters, paginated table with all job fields, expandable description rows, and direct links to job postings.""",

    17: """Replaced the FastAPI backend with a C# ASP.NET Core minimal API (webapp/api_cs/). Created Program.cs with /jobs, /countries, /stats endpoints using Microsoft.Data.Sqlite. Builds on .NET 10. Same three endpoints the React app already expects, so nothing changes on the frontend side.""",

    18: """The PostgreSQL ODBC driver has set OPENSSL_CONF to a file that doesn't exist. Fix: run $env:OPENSSL_CONF="" before npm commands, or add it to your PowerShell profile permanently to avoid doing it every time.""",

    19: """The DB path calculation was wrong - AppContext.BaseDirectory points to bin/Debug/net10.0/ at runtime. Changed to Directory.GetCurrentDirectory() which is webapp/api_cs/ when running dotnet run, so ../../SearchCode/jobs.db resolves correctly.""",

    20: """Added city and state columns to job_listings. Adzuna's area array for US jobs looks like ["USA", "South East", "Virginia", "Arlington"] - area[2] is the state. Created migrate_db.py to ALTER TABLE on the existing DB without losing data. Updated the job building code and db.py INSERT statement.""",
}

count = 0
for seq, response in responses.items():
    conn.execute(
        "UPDATE prompts_log SET response = ? WHERE sequence = ?",
        (response.strip(), seq)
    )
    count += 1

conn.commit()
print(f"Updated {count} responses (sequences 1-20).")
conn.close()
