"""Update prompts_log responses for sequences 41-70."""
import sqlite3
from pathlib import Path

DB_PATH = Path(__file__).parent / "jobs.db"
conn = sqlite3.connect(DB_PATH)

responses = {
    41: """Merged timezone into the location cell as a blue sub-label. Removed the separate Timezone column. Location now shows city/region on first line and EST offset (e.g. 'EST-3h') below it in blue.""",

    42: """Added DELETE /links/all endpoint that removes all processed links. Added red 'Clear All Imported' button with window.confirm() dialog to the Processed section of Import Links page.""",

    43: """Created useful_links table with id/description/url/added_at. Added full CRUD endpoints (GET/POST/PATCH/DELETE). Built UsefulLinks React page with description+URL input form, editable table with inline edit mode, and delete buttons. Added as third nav tab.""",

    44: """Added _detect_remote function that checks: 1) job criteria elements for workplace type, 2) country-only location pattern (no comma = remote on LinkedIn), 3) description text scanning for remote/hybrid/on-site keywords. Returns Remote/Hybrid/On-site or empty string.""",

    45: """Created test_fetch.py targeting job 4385168689. Found that LinkedIn's guest API shows no explicit 'Remote' label - the only signal is the location field showing just 'United States' with no city/state. No remote/hybrid keywords in the page text either.""",

    46: """Updated _detect_remote to accept a location parameter. Added check: if location has no comma and matches a known country name (United States, Canada, UK, etc.), classify as Remote. This matches LinkedIn's convention for remote job listings.""",

    47: """Created add_remote_column.py migration that adds is_remote TEXT column and back-fills existing rows by scanning title/location/job_type/description for remote/hybrid/on-site keywords. Updated db.py schema and INSERT statement.""",

    48: """Created update_remote_status.py that fetches each LinkedIn job via the guest API, runs the same detection logic, and updates is_remote only when changed. Supports --dry-run flag and --id for single job. Adds delay between requests for rate limiting.""",

    49: """Added colour-coded badge-remote next to job_type in the table. Green background for Remote, orange for Hybrid, blue for On-site. Wrapped in a type-cell div with flexbox.""",

    50: """Changed type-cell from horizontal flex (align-items: center) to vertical flex (flex-direction: column, align-items: flex-start) so job type text appears above the remote badge.""",

    51: """Added is_remote filter parameter to the /jobs API endpoint. Built collapsible dropdown panel with two sections: Type (radio buttons for job types) and Work Mode (radio buttons for Remote/Hybrid/On-site). Click outside to close. Button shows current selections.""",

    52: """Added POST /run-adzuna endpoint that executes adzuna_jobs.py with keywords argument. Built modal with editable keywords input, Run Search button, progress message, and output display. Auto-refreshes job list and stats on success. Green button in page header.""",

    53: """Added isNew() function comparing searched_at timestamp to Date.now() with 10-minute window. Orange pulsing 'New' badge appears under source badge. Component re-renders every 60 seconds via setInterval to expire badges automatically.""",

    54: """Created kanban_jobs table with job_listing_id FK, status, notes, updated_at. Built Kanban React page with 8 lanes, Card component with expand/collapse, move-to buttons, notes textarea, and save. GET /kanban endpoint auto-syncs missing kanban rows on load.""",

    55: """API was returning 108 cards correctly - the issue was CSS. The lane body had max-height cutting off content. Fixed with proper flex layout, min-height on board, and independent scroll per lane.""",

    56: """Updated LANES and LANE_COLORS arrays in Kanban.jsx from 'Researched' to 'Researching'. Created rename_lane.py migration script to UPDATE existing rows in the database.""",

    57: """Set kanban-page to flex column with calc(100vh - 52px) height. Header flex-shrink:0, board flex:1 with overflow. Lane headers sticky within their lane containers.""",

    58: """Added min-height:0 to flex children (board and lanes) - critical CSS property that allows flex items to shrink below their content size, enabling overflow scroll within nested flex containers.""",

    59: """Separated lane headers into a kanban-lane-headers-row div inside the sticky page header. Board only contains card bodies (no lane headers). Added scroll sync via useRef and onScroll handler to keep headers aligned with board horizontal scroll.""",

    60: """Removed sticky positioning from lane headers entirely. Page header stays fixed via position:sticky, lane headers sit naturally at top of each column. Tiles scroll under the page header - cleanest behaviour without complex layout.""",

    61: """Set gap:0 on both header row and board. Replaced gap with border-right: 1px solid #dde1e7 on each lane. Lines run full height of the board since lanes use align-items:stretch.""",

    62: """Both rows now use gap:0 so borders align perfectly. Added box-sizing:border-box to lane headers. Removed border-radius from lanes so separators are continuous top to bottom.""",

    63: """Added 'View →' link to kanban-card-badges row with margin-left:auto to push it right. onClick uses e.stopPropagation() to prevent card expand when clicking the link.""",

    64: """Added setExpanded(false) after successful note save in the saveNotes async function, so the card collapses back to its compact view after saving.""",

    65: """Added overflow-x:auto to .app:has(.kanban-page) and min-width:max-content to kanban-page so the full board width is scrollable horizontally.""",

    66: """Restructured to flex column layout. App container fills viewport with overflow:hidden. Kanban page is flex column - header fixed height at top, board fills remaining space with overflow-x/y:auto. Lane headers scroll in sync with board via onScroll handler and useRef.""",

    67: """Added HTML5 drag and drop using draggable attribute, onDragStart (sets card ID), onDragOver (preventDefault + highlight), onDragEnter, onDragLeave (with contains() check), onDrop (calls moveCard). Blue dashed outline on drop target. Saves immediately to DB on drop.""",

    68: """Created kanban_history table with kanban_id, from_status, to_status, changed_at. Every status change in PATCH /kanban/{id}/status logs an entry. Added History button on expanded card showing scrollable list of moves with timestamps.""",

    69: """Replaced table layout with stacked div entries. Each move shows 'From → To' on first line (with colour coding) and timestamp on second line in smaller grey text. Separated by light borders.""",

    70: """Changed API query to ORDER BY changed_at DESC (most recent first). Set max-height:138px on history panel (~3 entries visible at a time) with overflow-y:auto for scrolling through older entries.""",
}

count = 0
for seq, response in responses.items():
    conn.execute("UPDATE prompts_log SET response = ? WHERE sequence = ?", (response.strip(), seq))
    count += 1

conn.commit()
print(f"Updated {count} responses (sequences 41-70).")
conn.close()
