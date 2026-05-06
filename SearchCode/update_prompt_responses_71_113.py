"""Update prompts_log responses for sequences 71-113."""
import sqlite3
from pathlib import Path

DB_PATH = Path(__file__).parent / "jobs.db"
conn = sqlite3.connect(DB_PATH)

responses = {
    71: """Replaced the array of lane buttons with a single select dropdown. Fires moveCard immediately on change, resets to 'Select lane...' placeholder after each move. Much more compact in the narrow card width.""",

    72: """Added 'Application Error' to LANES array with dark red colour #b71c1c. Positioned between 'Not Heard Back (30d)' and 'Rejected by Employer'. No DB migration needed since it's just a new status value.""",

    73: """Added 'No Longer Accepting Applications' to end of LANES array with slate grey #546e7a. 10 lanes total now.""",

    74: """Merged 4 rejection lanes (Not Heard Back, Application Error, Rejected by Employer, No Longer Accepting Applications) into single 'Failed' lane. Added fail_type column to kanban_jobs. Created fail type selector dropdown in expanded card. Created add_fail_type_column.py migration that also moves existing rows to Failed with correct fail_type.""",

    75: """Migration script hadn't been run. Provided command: python SearchCode/add_fail_type_column.py. The CREATE TABLE IF NOT EXISTS in startup only creates new tables, doesn't alter existing ones.""",

    76: """Added FailTypeDialog modal component with radio button options for each fail type. Intercepts moves to Failed lane (both drag-drop and Move To dropdown). Must select a reason before confirming. Cancel leaves card in current lane. Saves fail_type to DB alongside the status change.""",

    77: """Created utils/anonymize.js with deterministic hash-based fake company names using adjective+noun combinations (e.g. 'Quantum Solutions', 'Apex Technologies'). Same company always gets same placeholder. Added checkbox toggle to page header. JobTable wraps company display in co() function.""",

    78: """Lifted anonymize state up to App.jsx so both pages share it. Passed as prop to Kanban component. Added co() wrapper function in Card component for the company display. Added anonymize toggle to the nav bar so it's accessible from any page.""",

    79: """Removed the local useState([anonymize, setAnonymize]) declaration from JobSearch.jsx since it now receives anonymize as a prop from App.jsx. The duplicate declaration was causing the 'already been declared' error.""",

    80: """Removed 'Rejected By Me' from LANES array, added it as first option in FAIL_TYPES array. Created migrate_rejected_by_me.py to UPDATE existing rows from status='Rejected By Me' to status='Failed', fail_type='Rejected By Me'. Board now has 6 lanes.""",

    81: """Added is_active INTEGER column to kanban_jobs. Created POST /kanban/{id}/active endpoint that toggles (clears all others first - only one can be active). Star button (☆/★) on Searched/Found cards only. Active card gets orange border, warm gradient background, and orange title.""",

    82: """Migration script needed to add the column to existing table. Provided: python SearchCode/add_active_column.py""",

    83: """Added check in PATCH /kanban/{id}/status: if fromStatus is 'Searched/Found' and new status is different, execute UPDATE kanban_jobs SET is_active = 0 WHERE id = $id. Clears the highlight automatically on lane change.""",

    84: """Added onDragEnter handler and changed onDragLeave to use e.currentTarget.contains(e.relatedTarget) check - only clears highlight when truly leaving the lane, not when moving over child elements. Added e.preventDefault() on onDrop. Set dataTransfer.setData for proper drag data.""",

    85: """Removed the CSS rule '.kanban-card[draggable]:active * { pointer-events: none; }' which was blocking all click events on card children including the expand header click handler.""",

    86: """Created kanban_notes table with kanban_id, note, created_at. Added GET/POST/DELETE endpoints. Rebuilt card notes section as a scrollable feed with individual timestamps, delete buttons per note, and a textarea to add new notes. Notes load on card expand. Ctrl+Enter to save.""",

    87: """Created POST /jobs/manual endpoint that generates a unique ID (manual_GUID), creates a search record, inserts the job listing, and auto-creates a kanban card at Searched/Found. Built AddJob React page with form fields for all columns (all optional). Added as nav tab.""",

    88: """Microsoft.Data.Sqlite doesn't expose LastInsertRowId as a property. Replaced with a separate command: SELECT last_insert_rowid() executed immediately after the INSERT.""",

    89: """Built Dashboard page with: stat cards (total jobs, applied, interviewed, researching, failed), current focus card (starred job), pipeline breakdown horizontal bar chart, jobs by country pills, and recent activity table (last 10 updated). Set as default tab on app startup.""",

    90: """Removed the total_searches stat card div from the dash-cards section in Dashboard.jsx.""",

    91: """Added /stats/applied-count endpoint querying COUNT(DISTINCT kanban_id) FROM kanban_history WHERE to_status='Applied'. Dashboard fetches this and uses it for the Applied stat card instead of counting jobs currently in the Applied lane.""",

    92: """Added /stats/outcomes endpoint. Built PieChart component using CSS conic-gradient with colour-coded legend showing counts and percentages for each outcome (current status or fail_type for failed jobs).""",

    93: """The query was filtering by kanban_history which missed some Failed jobs that didn't have an Applied history entry. Changed to simply show all jobs in Failed lane grouped by fail_type using COALESCE(fail_type, 'Unspecified').""",

    94: """Created backfill_applied_history.py that finds all Failed kanban_jobs without an Applied history entry and inserts one with from_status='Searched/Found', to_status='Applied'.""",

    95: """Updated /stats/outcomes query to include jobs in Applied/Interviewed/Accepted/Failed that have Applied history. Uses CASE WHEN status='Failed' THEN fail_type ELSE status END as the outcome grouping.""",

    96: """Provided the command: python SearchCode/backfill_applied_history.py""",

    97: """Replaced conic-gradient CSS pie chart with SVG-based donut chart using circle elements with stroke-dasharray and stroke-dashoffset. Total number displayed in center via SVG text elements. Added drop-shadow filter for depth.""",

    98: """Made SVG segments and legend items clickable via onClick handlers. Clicking opens a modal overlay showing a filtered table of jobs matching that outcome (title, company, location, source). Click outside modal to dismiss. Added hover opacity on segments and background on legend items.""",

    99: """Passed anonymize prop from App through Dashboard to PieChart. Applied anonFn() to company field in: drilldown modal table, recent activity table, and current focus card.""",

    100: """Diagnosed the issue: applications were on May 5 EST but the API checked against May 6 UTC. Created check_applied_today.py diagnostic script showing the last 10 Applied history entries and the UTC date comparison. Led to the Settings/timezone solution.""",

    101: """Created settings table with key/value pairs. Added GET/PATCH /settings endpoints. Updated /stats/applied-today to read timezone from settings and convert UTC now to user's local date. Built Settings React page with timezone selection. Default: America/New_York.""",

    102: """Replaced the scrollable radio button list with a single HTML select element populated with Intl.supportedValuesOf('timeZone'). Saves immediately on change.""",

    103: """Added /stats/applied-today endpoint that uses the user's timezone setting to determine 'today'. Built widget with bouncing happy emoji (😊) + count when > 0, or sad emoji (😞) when 0. Positioned next to pie chart in a flex row.""",

    104: """Updated PATCH /kanban/{id}/status to check: if moving to Failed from Searched/Found or Researching, auto-insert a kanban_history entry with to_status='Applied' before the Failed entry. This ensures the applied-count stat stays accurate.""",

    105: """Added US_STATES dictionary (full name to abbreviation), _extract_state_from_area (scans Adzuna area array for known state names), and _geocode_state (calls Nominatim OpenStreetMap API with 1 req/sec rate limit). Created backfill_states.py for existing rows. Results cached within a run.""",

    106: """Created app_metadata/prompts.csv with columns: sequence, date, prompt_summary, category. Listed all 106 prompts from the conversation in chronological order.""",

    107: """Added /prompts GET endpoint that reads the CSV file, skips header, splits on commas. Built PromptsLog React page with filterable table, colour-coded category badges, and sticky header. Added as nav tab.""",

    108: """Agreed to append a new line to prompts.csv with sequence number, current date/time, prompt summary, category, and response after every change going forward.""",

    109: """Appended entries 109-121 to the CSV covering the timezone dropdown, happy face widget, settings page, and related fixes that were done but not logged.""",

    110: """Added response as 5th CSV column. Updated C# endpoint to split on 5 commas (response may contain commas). Built ResponseCell component showing first line with 'more' button that expands to show full response text in a scrollable area.""",

    111: """Rewrote entire prompts.csv with detailed response summaries for all entries. Each response describes what was done in 1-3 sentences.""",

    112: """Removed Response column from PromptsLog table. Made rows clickable with cursor:pointer and hover highlight. Added modal overlay showing full prompt text and response. Click outside to dismiss. Styled with scrollable response area on dark background.""",

    113: """Added prompts_log table to C# startup CREATE TABLE block. Updated /prompts endpoint to SELECT from the table instead of parsing CSV. Created import_prompts_to_db.py migration script that reads CSV and inserts all rows. Keeps CSV as backup.""",
}

count = 0
for seq, response in responses.items():
    conn.execute("UPDATE prompts_log SET response = ? WHERE sequence = ?", (response.strip(), seq))
    count += 1

conn.commit()
print(f"Updated {count} responses (sequences 71-113).")
conn.close()
