# Development Guide

## Development Workflow

### Project Structure Overview

This is a full-stack application with:

- **Backend**: C# ASP.NET Core Web API (port 5300)
- **Frontend**: React + Vite (port 5173)
- **Database**: SQLite (shared file)
- **Scripts**: Python for scraping
- **Extension**: Chrome/Edge browser extension

### Development Server Setup

Run both backend and frontend concurrently:

**Terminal 1 - Backend**:

```bash
cd webapp/api_cs
dotnet run
```

**Terminal 2 - Frontend**:

```bash
cd webapp/client
npm run dev
```

Access the application:

- Frontend: `http://localhost:5173`
- Backend API: `http://localhost:5300`
- Swagger UI: `http://localhost:5300/swagger`

## Backend Development (C#)

### Project Structure

```
webapp/api_cs/
├── Controllers/          # API controllers (REST endpoints)
│   ├── JobsController.cs
│   ├── KanbanController.cs
│   ├── PromptsController.cs
│   ├── SettingsController.cs
│   ├── StatsController.cs
│   ├── LinksController.cs
│   ├── UsefulLinksController.cs
│   ├── AdzunaController.cs
│   ├── CodeStatsController.cs
│   └── KuriaController.cs
├── Data/
│   └── JobSearchDatabase.cs  # Database initialization
├── Program.cs            # Application entry point
└── JobApi.csproj         # Project file
```

### Adding a New Controller

1. Create a new file in `Controllers/`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using JobSearchAPI.Data;

namespace JobSearchAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("YourFeature")]
public class YourController : ControllerBase
{
    private readonly JobSearchDatabase _db;

    public YourController(JobSearchDatabase db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetItems()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM your_table";

        var items = new List<Dictionary<string, object?>>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                items.Add(row);
            }
        }

        return Ok(items);
    }
}
```

2. The controller will be automatically discovered by `app.MapControllers()` in `Program.cs`

### Database Patterns

#### Creating a New Table

Add the table creation SQL to `JobSearchDatabase.Initialize()`:

```csharp
cmd.CommandText = @"
    -- Your new table
    CREATE TABLE IF NOT EXISTS your_table (
        id          INTEGER PRIMARY KEY AUTOINCREMENT,
        name        TEXT    NOT NULL,
        created_at  TEXT    NOT NULL
    );
";
```

#### Common Database Operations

**Insert**:

```csharp
using var cmd = conn.CreateCommand();
cmd.CommandText = "INSERT INTO your_table (name, created_at) VALUES (@name, @at)";
cmd.Parameters.AddWithValue("@name", name);
cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
await cmd.ExecuteNonQueryAsync();
```

**Select with Parameters**:

```csharp
using var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT * FROM your_table WHERE id = @id";
cmd.Parameters.AddWithValue("@id", id);
using var reader = await cmd.ExecuteReaderAsync();
```

**Update**:

```csharp
using var cmd = conn.CreateCommand();
cmd.CommandText = "UPDATE your_table SET name = @name WHERE id = @id";
cmd.Parameters.AddWithValue("@name", name);
cmd.Parameters.AddWithValue("@id", id);
await cmd.ExecuteNonQueryAsync();
```

**Delete**:

```csharp
using var cmd = conn.CreateCommand();
cmd.CommandText = "DELETE FROM your_table WHERE id = @id";
cmd.Parameters.AddWithValue("@id", id);
await cmd.ExecuteNonQueryAsync();
```

### Error Handling

Always return appropriate HTTP status codes:

```csharp
// Validation error
if (string.IsNullOrEmpty(name))
    return BadRequest(new { error = "name is required" });

// Not found
var item = await GetItem(id);
if (item == null)
    return NotFound();

// Success
return Ok(item);

// Created
return CreatedAtAction(nameof(GetItem), new { id }, item);
```

### Testing Controllers

Use the Swagger UI at `http://localhost:5300/swagger` to test endpoints interactively.

Or use curl:

```bash
# GET request
curl http://localhost:5300/api/jobs

# POST request
curl -X POST http://localhost:5300/api/jobs/manual \
  -H "Content-Type: application/json" \
  -d '{"title":"Software Engineer","company":"Tech Corp"}'

# PATCH request
curl -X PATCH http://localhost:5300/api/kanban/1/status \
  -H "Content-Type: application/json" \
  -d '{"status":"Applied"}'
```

## Frontend Development (React)

### Project Structure

```
webapp/client/src/
├── pages/               # Page components
│   ├── Dashboard.jsx
│   ├── JobSearch.jsx
│   ├── Kanban.jsx
│   ├── AddJob.jsx
│   ├── ImportLinks.jsx
│   ├── UsefulLinks.jsx
│   ├── CodeMap.jsx
│   ├── PromptsLog.jsx
│   └── Settings.jsx
├── components/          # Reusable components
│   ├── JobTable.jsx
│   ├── Filters.jsx
│   ├── StatsBar.jsx
│   ├── Bookmarklet.jsx
│   └── SwaggerDocs.jsx
├── utils/               # Utility functions
│   ├── anonymize.js
│   └── timezone.js
├── App.jsx              # Main app component
├── App.css              # Global styles
├── main.jsx             # Entry point
└── index.css            # Base styles
```

### Adding a New Page

1. Create the page component in `pages/`:

```jsx
import { useState, useEffect } from "react";

export default function YourPage() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const res = await fetch("/api/your-endpoint");
      const json = await res.json();
      setData(json);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div>Loading...</div>;

  return (
    <div className="your-page">
      <h1>Your Page</h1>
      {/* Your content */}
    </div>
  );
}
```

2. Add the route in `App.jsx`:

```jsx
import YourPage from './pages/YourPage'

// In the component:
const [page, setPage] = useState('dashboard')

// Add navigation button:
<button
  className={page === 'yourpage' ? 'nav-link active' : 'nav-link'}
  onClick={() => setPage('yourpage')}
>
  Your Page
</button>

// Add page rendering:
{page === 'yourpage' && <YourPage />}
```

### API Calls

Use the fetch API with the Vite proxy:

```jsx
// GET request
const response = await fetch("/api/jobs?keywords=software");
const data = await response.json();

// POST request
const response = await fetch("/api/jobs/manual", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    title: "Software Engineer",
    company: "Tech Corp",
  }),
});
const result = await response.json();

// PATCH request
const response = await fetch("/api/kanban/1/status", {
  method: "PATCH",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ status: "Applied" }),
});
```

### State Management

The app uses a simple state management pattern:

**Global State** (in App.jsx):

```jsx
const [page, setPage] = useState("dashboard");
const [anonymize, setAnonymize] = useState(false);
```

**Local State** (in pages/components):

```jsx
const [jobs, setJobs] = useState([]);
const [filters, setFilters] = useState({});
```

**String Management** (pub/sub pattern):

```jsx
import { useAppStrings, getString } from '../hooks/useAppStrings'

const { s } = useAppStrings()
// Use: s('PageName', 'stringKey', 'Fallback')
<h1>{s('Dashboard', 'title', 'Dashboard')}</h1>
```

### Styling

The app uses custom CSS in `App.css` and `index.css`. Follow these conventions:

- Use BEM-like naming: `.component-name`, `.component-name--modifier`
- Use CSS custom properties for theming
- Keep styles component-scoped
- Use flexbox/grid for layouts

Example:

```css
.your-page {
  padding: 20px;
}

.your-page__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.your-page__button {
  padding: 8px 16px;
  background: #007bff;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.your-page__button:hover {
  background: #0056b3;
}
```

## Python Scripts Development

### Project Structure

```
SearchCode/
├── adzuna_jobs.py       # Adzuna API scraper
├── indeed_jobs.py       # Indeed scraper
├── linkedin_jobs.py     # LinkedIn scraper
├── linkedin_import.py   # LinkedIn import utility
├── db.py                # Database utilities
├── jobs.db              # SQLite database
└── [other scripts]      # Various utility scripts
```

### Database Access Pattern

```python
import sqlite3
from datetime import datetime

def get_connection():
    return sqlite3.connect('jobs.db')

def insert_job(conn, job_data):
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO job_listings
        (id, search_id, searched_at, title, company, location, country)
        VALUES (?, ?, ?, ?, ?, ?, ?)
    """, (
        job_data['id'],
        job_data['search_id'],
        datetime.utcnow().isoformat(),
        job_data['title'],
        job_data['company'],
        job_data['location'],
        job_data['country']
    ))
    conn.commit()

# Usage
conn = get_connection()
try:
    insert_job(conn, job_data)
finally:
    conn.close()
```

### Adding a New Scraper

1. Create a new Python file (e.g., `new_scraper.py`)
2. Follow the existing pattern:
   - Connect to database
   - Fetch data from API/website
   - Transform data to match schema
   - Insert into database
   - Handle errors gracefully

Example structure:

```python
import sqlite3
import requests
from datetime import datetime

DB_PATH = 'jobs.db'

def scrape_jobs(keywords, location):
    """Scrape jobs from API/website"""
    jobs = []

    # Your scraping logic here
    # response = requests.get('https://api.example.com/jobs', params={...})
    # jobs = parse_response(response)

    return jobs

def save_jobs(jobs):
    """Save jobs to database"""
    conn = sqlite3.connect(DB_PATH)
    try:
        for job in jobs:
            save_job(conn, job)
        conn.commit()
    finally:
        conn.close()

def save_job(conn, job):
    """Save a single job"""
    cursor = conn.cursor()
    cursor.execute("""
        INSERT OR IGNORE INTO job_listings
        (id, search_id, searched_at, title, company, location, country, url, source)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, (
        job['id'],
        job['search_id'],
        datetime.utcnow().isoformat(),
        job['title'],
        job['company'],
        job['location'],
        job['country'],
        job['url'],
        job['source']
    ))

if __name__ == '__main__':
    jobs = scrape_jobs('Software Engineer', 'United States')
    save_jobs(jobs)
    print(f"Scraped {len(jobs)} jobs")
```

## Browser Extension Development

### Project Structure

```
browser-extension/
├── manifest.json       # Extension configuration
├── background.js       # Service worker
├── offscreen.html      # Offscreen document
├── offscreen.js        # URL extraction logic
├── icon.png            # Extension icon
└── icon.svg            # Extension icon (vector)
```

### Modifying the Extension

1. Edit the files in `browser-extension/`
2. Reload the extension in Chrome/Edge:
   - Go to `chrome://extensions/`
   - Find your extension
   - Click the refresh button

### Key Concepts

**Manifest V3**: The extension uses Manifest V3 with service workers.

**Background Script**: Handles events and messaging:

```javascript
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === "PROCESS_URL") {
    // Process the URL
    sendResponse({ success: true });
  }
  return true; // Keep message channel open for async response
});
```

**Offscreen Document**: Used for operations that require DOM access:

```javascript
// offscreen.js
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === "EXTRACT_URL") {
    const url = window.location.href;
    sendResponse({ url });
  }
});
```

## Code Style Guidelines

### C# Style

- Use **var** for local variables when type is obvious
- Use **async/await** for all I/O operations
- Use **parameterized queries** to prevent SQL injection
- Follow **nullable reference types** (`string?`, `string`)
- Use **expression-bodied members** for simple methods
- Prefer **using declarations** over `using` statements

Example:

```csharp
[HttpGet]
public async Task<IActionResult> GetJobs(string? keywords)
{
    if (string.IsNullOrWhiteSpace(keywords))
        return Ok(new List<object>());

    using var conn = _db.CreateConnection();
    await conn.OpenAsync();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM job_listings WHERE title LIKE @kw";
    cmd.Parameters.AddWithValue("@kw", $"%{keywords}%");

    // ... rest of implementation
}
```

### JavaScript/React Style

- Use **functional components** with hooks
- Use **ES6+** syntax (arrow functions, destructuring, etc.)
- Use **const/let** (never var)
- Use **async/await** for async operations
- Follow **camelCase** for variables/functions
- Follow **PascalCase** for components

Example:

```jsx
import { useState, useEffect } from "react";

export default function JobTable({ jobs, onDelete }) {
  const [sortBy, setSortBy] = useState("date_posted");

  useEffect(() => {
    console.log("Jobs updated:", jobs.length);
  }, [jobs]);

  const handleDelete = async (id) => {
    if (!confirm("Delete this job?")) return;

    try {
      const res = await fetch(`/api/jobs/${id}`, { method: "DELETE" });
      if (res.ok) onDelete(id);
    } catch (err) {
      console.error("Delete failed:", err);
    }
  };

  return (
    <table className="job-table">
      <thead>
        <tr>
          <th>Title</th>
          <th>Company</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        {jobs.map((job) => (
          <tr key={job.id}>
            <td>{job.title}</td>
            <td>{job.company}</td>
            <td>
              <button onClick={() => handleDelete(job.id)}>Delete</button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
```

### Python Style

- Follow **PEP 8** style guide
- Use **type hints** where appropriate
- Use **context managers** for database connections
- Handle **exceptions** gracefully
- Use **docstrings** for functions/classes

Example:

```python
import sqlite3
from typing import List, Dict, Optional

def get_jobs(conn: sqlite3.Connection, country: Optional[str] = None) -> List[Dict]:
    """
    Retrieve jobs from the database, optionally filtered by country.

    Args:
        conn: Database connection
        country: Optional country filter

    Returns:
        List of job dictionaries
    """
    cursor = conn.cursor()

    if country:
        cursor.execute(
            "SELECT * FROM job_listings WHERE country = ?",
            (country,)
        )
    else:
        cursor.execute("SELECT * FROM job_listings")

    columns = [desc[0] for desc in cursor.description]
    return [dict(zip(columns, row)) for row in cursor.fetchall()]

# Usage
with sqlite3.connect('jobs.db') as conn:
    jobs = get_jobs(conn, country='United States')
    for job in jobs:
        print(job['title'])
```

## Git Workflow

### Branching Strategy

- **main** - Production-ready code
- **develop** - Integration branch (optional)
- **feature/\*** - New features
- **bugfix/\*** - Bug fixes
- **hotfix/\*** - Urgent production fixes

### Commit Messages

Follow conventional commits format:

```
type(scope): description

[optional body]

[optional footer]
```

**Types**:

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding/updating tests
- `chore`: Maintenance tasks

**Examples**:

```
feat(kanban): add timer tracking for job applications
fix(jobs): resolve pagination issue on search results
docs(api): update JobsController documentation
refactor(db): optimize database connection pooling
```

### Development Workflow

```bash
# 1. Create feature branch
git checkout -b feature/add-job-export

# 2. Make changes and commit
git add .
git commit -m "feat(jobs): add CSV export functionality"

# 3. Push to remote
git push origin feature/add-job-export

# 4. Create pull request (via GitHub CLI or web interface)
gh pr create --title "Add job export feature" --body "Implements CSV export for job listings"

# 5. After review, merge to main
git checkout main
git merge feature/add-job-export
git push origin main
```

## Testing

### Backend Testing

Currently, there are no automated tests. To add tests:

1. Create a test project:

```bash
cd webapp/api_cs
dotnet new xunit -n JobApi.Tests
cd JobApi.Tests
dotnet add reference ../JobApi.csproj
```

2. Write tests:

```csharp
using Xunit;
using JobSearchAPI.Controllers;
using Microsoft.Data.Sqlite;

public class JobsControllerTests
{
    [Fact]
    public async Task GetJobs_ReturnsOkResult()
    {
        // Arrange
        var db = new JobSearchDatabase();
        var controller = new JobsController(db);

        // Act
        var result = await controller.GetJobs(null, null, null, null, null, null, null, null);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
```

3. Run tests:

```bash
dotnet test
```

### Frontend Testing

To add frontend tests:

1. Install testing libraries:

```bash
cd webapp/client
npm install --save-dev vitest @testing-library/react @testing-library/jest-dom
```

2. Create test files:

```jsx
// JobTable.test.jsx
import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import JobTable from "./JobTable";

describe("JobTable", () => {
  it("renders job listings", () => {
    const jobs = [{ id: "1", title: "Engineer", company: "Tech Corp" }];
    render(<JobTable jobs={jobs} />);

    expect(screen.getByText("Engineer")).toBeDefined();
    expect(screen.getByText("Tech Corp")).toBeDefined();
  });

  it("calls onDelete when delete button clicked", async () => {
    const onDelete = vi.fn();
    const jobs = [{ id: "1", title: "Engineer", company: "Tech Corp" }];
    render(<JobTable jobs={jobs} onDelete={onDelete} />);

    fireEvent.click(screen.getByText("Delete"));
    expect(onDelete).toHaveBeenCalledWith("1");
  });
});
```

3. Run tests:

```bash
npm test
```

## Debugging

### Backend Debugging

**Visual Studio**:

- Set breakpoints in Controllers
- Press F5 to start debugging
- API will start with debugger attached

**Visual Studio Code**:

1. Install C# extension
2. Set breakpoints
3. Press F5 or run "Debug: Start Debugging"
4. Select ".NET Core" environment

**Console Logging**:

```csharp
Console.WriteLine($"Debug: {variable}");
Console.Error.WriteLine($"Error: {error}");
```

### Frontend Debugging

**Browser DevTools**:

- Open DevTools (F12)
- Console tab for logs
- Network tab for API calls
- React DevTools extension for component inspection

**VS Code Debugger**:

1. Install Debugger for Chrome extension
2. Create `.vscode/launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "chrome",
      "request": "launch",
      "name": "Launch Chrome",
      "url": "http://localhost:5173",
      "webRoot": "${workspaceFolder}/webapp/client/src"
    }
  ]
}
```

**Console Logging**:

```jsx
console.log("Debug:", data);
console.error("Error:", error);
console.table(jobs); // For tabular data
```

## Performance Optimization

### Backend

1. **Database Indexes**: Add indexes for frequently queried columns
2. **Connection Pooling**: Consider adding connection pooling for production
3. **Query Optimization**: Use EXPLAIN QUERY PLAN to optimize slow queries
4. **Pagination**: Always use pagination for large result sets
5. **Caching**: Add Redis caching for frequently accessed data

### Frontend

1. **Code Splitting**: Use React.lazy() for route-based splitting
2. **Memoization**: Use React.memo() and useMemo() for expensive computations
3. **Virtualization**: Use react-window for long lists
4. **Image Optimization**: Compress images, use WebP format
5. **Bundle Analysis**: Use `npm run build` and analyze bundle size

## Common Tasks

### Adding a New API Endpoint

1. Add method to appropriate Controller
2. Test with Swagger UI
3. Update API.md documentation
4. Add frontend page/component if needed

### Adding a New Database Table

1. Add CREATE TABLE to `JobSearchDatabase.Initialize()`
2. Create/update Controller methods
3. Add frontend UI if needed
4. Update ARCHITECTURE.md

### Modifying the Database Schema

1. Update `JobSearchDatabase.Initialize()` with ALTER TABLE or new CREATE TABLE
2. Consider data migration for existing databases
3. Update all affected Controllers
4. Test thoroughly

### Adding a New Python Script

1. Create script in `SearchCode/`
2. Follow existing patterns for database access
3. Add error handling and logging
4. Test with sample data

## Deployment Checklist

Before deploying to production:

- [ ] Update connection strings for production database
- [ ] Enable HTTPS
- [ ] Restrict CORS to specific origins
- [ ] Add authentication/authorization
- [ ] Configure logging (Serilog)
- [ ] Add health check endpoints
- [ ] Set up monitoring and alerts
- [ ] Configure automated backups
- [ ] Run security scan
- [ ] Test all critical paths
- [ ] Update documentation
- [ ] Tag release in Git

## Useful Commands

### Backend

```bash
# Run with specific environment
dotnet run --environment Production

# Build for release
dotnet build -c Release

# Publish self-contained
dotnet publish -c Release -r win-x64 --self-contained

# Watch mode (auto-reload)
dotnet watch run

# Restore packages
dotnet restore

# Clean build artifacts
dotnet clean
```

### Frontend

```bash
# Development server
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview

# Lint code
npm run lint

# Install dependencies
npm install

# Update dependencies
npm update
```

### Database

```bash
# Open SQLite shell
sqlite3 SearchCode/jobs.db

# List tables
.tables

# Show table schema
.schema job_listings

# Query data
SELECT * FROM job_listings LIMIT 10;

# Export database
sqlite3 jobs.db .dump > backup.sql

# Import database
sqlite3 jobs.db < backup.sql

# Exit
.quit
```

### Git

```bash
# View commit history
git log --oneline --graph --all

# View changes
git diff

# Stage specific files
git add path/to/file

# Amend last commit
git commit --amend

# Undo last commit (keep changes)
git reset --soft HEAD~1

# Undo last commit (discard changes)
git reset --hard HEAD~1

# Stash changes
git stash
git stash pop

# View branches
git branch -a

# Delete branch
git branch -d feature/old-feature
```

## Resources

### Documentation

- [ASP.NET Core Docs](https://docs.microsoft.com/aspnet/core)
- [React Docs](https://react.dev/)
- [Vite Docs](https://vitejs.dev/)
- [SQLite Docs](https://www.sqlite.org/docs.html)
- [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/)

### Tools

- [Swagger Editor](https://editor.swagger.io/)
- [SQLite Browser](https://sqlitebrowser.org/)
- [Postman](https://www.postman.com/) (API testing)
- [React DevTools](https://react.dev/learn/react-developer-tools)

### Learning

- [C# Documentation](https://docs.microsoft.com/dotnet/csharp/)
- [React Tutorial](https://react.dev/learn)
- [Vite Guide](https://vitejs.dev/guide/)
