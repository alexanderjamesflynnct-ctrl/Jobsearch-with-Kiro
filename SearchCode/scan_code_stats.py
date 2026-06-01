"""
Scan the workspace and populate code_stats table with line counts by file type.
Run from workspace root: py -3.14 SearchCode/scan_code_stats.py
"""
import os
import sqlite3
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

DB_PATH = Path(__file__).parent / "jobs.db"
WORKSPACE = Path(__file__).parent.parent

# Directories to skip
SKIP_DIRS = {'node_modules', '.git', 'bin', 'obj', '__pycache__', '.vscode'}

# File extension to language mapping
EXT_MAP = {
    '.py':    'Python',
    '.cs':    'C#',
    '.jsx':   'React (JSX)',
    '.js':    'JavaScript',
    '.css':   'CSS',
    '.html':  'HTML',
    '.json':  'JSON',
    '.csv':   'CSV',
    '.csproj':'C# Project',
    '.md':    'Markdown',
    '.txt':   'Text',
    '.http':  'HTTP',
    '.svg':   'SVG',
    '.cjs':   'JavaScript (CJS)',
}


def scan():
    stats = defaultdict(lambda: {'files': 0, 'lines': 0})
    global all_files
    all_files = []

    for root, dirs, files in os.walk(WORKSPACE):
        # Skip ignored directories
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]

        for fname in files:
            fpath = Path(root) / fname
            ext = fpath.suffix.lower()

            if ext not in EXT_MAP:
                file_type = f'Other ({ext})' if ext else 'Other (no ext)'
            else:
                file_type = EXT_MAP[ext]

            try:
                lines = len(fpath.read_text(encoding='utf-8', errors='ignore').splitlines())
            except Exception:
                lines = 0

            stats[file_type]['files'] += 1
            stats[file_type]['lines'] += lines

            # Store relative path
            rel_path = str(fpath.relative_to(WORKSPACE)).replace('\\', '/')
            all_files.append({'path': rel_path, 'type': file_type, 'lines': lines})

    # Sort files by line count descending
    all_files.sort(key=lambda x: -x['lines'])
    return stats


def save(stats):
    conn = sqlite3.connect(DB_PATH)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS code_stats (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            file_type  TEXT    NOT NULL,
            file_count INTEGER NOT NULL DEFAULT 0,
            line_count INTEGER NOT NULL DEFAULT 0,
            scanned_at TEXT    NOT NULL
        )
    """)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS code_files (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            file_path  TEXT    NOT NULL,
            file_type  TEXT    NOT NULL,
            line_count INTEGER NOT NULL DEFAULT 0,
            scanned_at TEXT    NOT NULL
        )
    """)

    # Clear previous scan
    conn.execute("DELETE FROM code_stats")
    conn.execute("DELETE FROM code_files")

    scanned_at = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    total_files = 0
    total_lines = 0

    for file_type, counts in sorted(stats.items(), key=lambda x: -x[1]['lines']):
        conn.execute(
            "INSERT INTO code_stats (file_type, file_count, line_count, scanned_at) VALUES (?,?,?,?)",
            (file_type, counts['files'], counts['lines'], scanned_at)
        )
        total_files += counts['files']
        total_lines += counts['lines']
        print(f"  {file_type:<20} {counts['files']:>4} files  {counts['lines']:>6} lines")

    # Save individual files
    for file_info in all_files:
        conn.execute(
            "INSERT INTO code_files (file_path, file_type, line_count, scanned_at) VALUES (?,?,?,?)",
            (file_info['path'], file_info['type'], file_info['lines'], scanned_at)
        )

    conn.commit()
    conn.close()

    print(f"\n  {'TOTAL':<20} {total_files:>4} files  {total_lines:>6} lines")
    print(f"\nSaved to code_stats and code_files tables.")


if __name__ == "__main__":
    print("Scanning workspace...\n")
    stats = scan()
    save(stats)
