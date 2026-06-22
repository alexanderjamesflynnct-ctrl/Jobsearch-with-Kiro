# Architecture Documentation

## System Overview

The Job Search Database Application follows a client-server architecture with three main components:

1. **Backend API** (C# ASP.NET Core) - RESTful API server
2. **Frontend Client** (React) - Single-page application
3. **Database** (SQLite) - Shared data store
4. **Supporting Tools** - Python scripts, browser extension, desktop launcher

## Architecture Diagram

```
┌─────────────────┐
│   React Client  │ (Port 5173 - Vite dev server)
│   (Browser)     │
└────────┬────────┘
         │ HTTP/REST
         │
┌────────▼────────────────────────────────────────┐
│   C# ASP.NET Core Web API                        │
│   (Port 5300)                                    │
│                                                  │
│  ┌──────────────────────────────────────────┐  │
│  │  Controllers Layer                        │  │
│  │  - JobsController                         │  │
│  │  - KanbanController                       │  │
│  │  - PromptsController                      │  │
│  │  - SettingsController                     │  │
│  │  - StatsController                        │  │
│  │  - LinksController                        │  │
│  │  - UsefulLinksController                  │  │
│  │  - AdzunaController                       │  │
│  │  - CodeStatsController                    │  │
│  │  - KuriaController                        │  │
│  └──────────────────────────────────────────┘  │
│                                                  │
│  ┌──────────────────────────────────────────┐  │
│  │  Data Access Layer                         │  │
│  │  - JobSearchDatabase (ADO.NET)            │  │
│  └──────────────────────────────────────────┘  │
└────────┬────────────────────────────────────────┘
         │
         │ SQLite Connection
         │
┌────────▼────────────────────────────────────────┐
│   SQLite Database                                │
│   (SearchCode/jobs.db)                           │
│                                                  │
│   Tables:                                        │
│   - searches                                     │
│   - job_listings                                 │
│   - job_links                                    │
│   - kanban_jobs                                  │
│   - kanban_history                               │
│   - kanban_notes                                 │
│   - job_timers                                   │
│   - settings                                     │
│   - prompts_log                                  │
│   - code_stats                                   │
│   - useful_links                                 │
└──────────────────────────────────────────────────┘

┌─────────────────┐
│ Python Scripts  │ (SearchCode/)
│ - adzuna_jobs.py │
│ - indeed_jobs.py │
│ - linkedin_jobs  │
└────────┬────────┘
         │
         │ Direct DB Access
         │
└────────▼────────────────────────────────────────┐
│   SQLite Database (same jobs.db)                 │
└──────────────────────────────────────────────────┘

┌─────────────────┐
│ Browser Extension│
│ - background.js  │
│ - offscreen.js   │
└────────┬────────┘
         │
         │ URL Capture
         │
└────────▼────────────────────────────────────────┐
│   C# API (LinksController)                       │
└──────────────────────────────────────────────────┘
```

## Backend Architecture

### Technology Stack

- **Framework**: ASP.NET Core Web API (.NET 10)
- **Database**: SQLite with Microsoft.Data.Sqlite
- **API Documentation**: Swashbuckle.AspNetCore + .NET 10 Native OpenAPI
- **Data Access**: ADO.NET (raw SQL queries)

### Application Structure

#### Program.cs

- Application entry point
- Service registration (Controllers, CORS, Swagger, Database)
- Middleware pipeline configuration
- Database initialization on startup
- Listens on `http://localhost:5300`

#### Controllers Layer

All controllers follow the same pattern:

- Inherit from `ControllerBase`
- Use `[ApiController]` and `[Route("api/[controller]")]` attributes
- Inject `JobSearchDatabase` singleton
- Use ADO.NET with parameterized queries
- Return `IActionResult` with appropriate HTTP status codes

**Key Controllers:**

- **JobsController**: CRUD operations for job listings, search/filter, manual entry, stale job cleanup
- **KanbanController**: Pipeline management, status updates, history tracking, notes, timers
- **PromptsController**: AI prompt logging and retrieval
- **SettingsController**: Application configuration management
- **StatsController**: Dashboard statistics and metrics
- **LinksController**: Job URL import and processing
- **UsefulLinksController**: Resource link management
- **AdzunaController**: Adzuna API integration
- **CodeStatsController**: Codebase statistics
- **KuriaController**: Custom endpoints

#### Data Access Layer

- **JobSearchDatabase**: Singleton service
  - Creates SQLite connections
  - Initializes database schema on startup
  - Provides connection factory method
  - Database path: `SearchCode/jobs.db` (relative to API runtime)

### Database Design

#### Schema Overview

The database uses a relational schema with foreign key relationships:

**Core Tables:**

- `searches` - Tracks search queries and timestamps
- `job_listings` - Main job data with full metadata
- `job_links` - Queue for imported URLs awaiting processing

**Kanban Tables:**

- `kanban_jobs` - Links jobs to pipeline status
- `kanban_history` - Audit trail of status changes
- `kanban_notes` - User notes on jobs
- `job_timers` - Time tracking per job

**Supporting Tables:**

- `settings` - Key-value configuration store
- `prompts_log` - AI prompt history
- `code_stats` - Codebase metrics
- `useful_links` - Saved resources

#### Key Relationships

```
searches (1) ──< (N) job_listings
job_listings (1) ──< (N) kanban_jobs
kanban_jobs (1) ──< (N) kanban_history
kanban_jobs (1) ──< (N) kanban_notes
kanban_jobs (1) ──< (N) job_timers
```

#### Indexes

- `idx_kanban_status` on `kanban_jobs(status)`
- `idx_history_kanban_id` on `kanban_history(kanban_id)`
- `idx_notes_kanban_id` on `kanban_notes(kanban_id)`
- `idx_timers_kanban_id` on `job_timers(kanban_id)`

## Frontend Architecture

### Technology Stack

- **Framework**: React 18
- **Routing**: react-router-dom v7
- **Build Tool**: Vite
- **UI**: Custom CSS
- **State Management**: React hooks with pub/sub pattern

### Application Structure

#### App.jsx

- Main application component
- Client-side routing via state (not react-router)
- Global state: current page, anonymization toggle
- Navigation bar with page buttons
- Anonymization toggle for sensitive data

#### Pages

Each page is a self-contained component:

- **Dashboard**: Overview statistics and metrics
- **JobSearch**: Job listing table with filters
- **Kanban**: Pipeline board with drag-and-drop
- **AddJob**: Manual job entry form
- **ImportLinks**: Bulk URL import interface
- **UsefulLinks**: Resource link management
- **CodeMap**: Codebase visualization
- **PromptsLog**: AI prompt history viewer
- **Settings**: Application configuration

#### Components

Reusable UI components:

- **JobTable**: Displays job listings with sorting/filtering
- **Filters**: Filter controls for job search
- **StatsBar**: Statistics display
- **Bookmarklet**: Browser bookmarklet generator
- **SwaggerDocs**: Embedded Swagger UI

#### Utilities

- **anonymize.js**: Data anonymization functions
- **timezone.js**: Timezone conversion utilities

### State Management Pattern

The application uses a custom pub/sub pattern for string management:

```javascript
// Global string cache with version tracking
let cachedStrings: Map<string, string> | null = null
let version = 0
const listeners = new Set<() => void>()

// Components subscribe to changes
const useAppStrings = () => {
  const [, setVer] = useState(version)
  useEffect(() => {
    const listener = () => setVer(v => v + 1)
    listeners.add(listener)
    return () => { listeners.delete(listener) }
  }, [])
  return { ready: cachedStrings !== null, s: getString }
}
```

This pattern enables:

- Centralized string management
- Live updates across all components
- No prop drilling or context overhead

## Data Flow

### Job Search Flow

1. User enters search criteria in frontend
2. Frontend sends GET request to `/api/jobs` with query parameters
3. JobsController builds dynamic SQL WHERE clause
4. Database executes parameterized query
5. Results returned as JSON with pagination metadata
6. Frontend renders JobTable component

### Kanban Update Flow

1. User drags job to new status
2. Frontend sends PATCH request to `/api/kanban/{id}/status`
3. KanbanController:
   - Retrieves current status
   - Updates status and timestamp
   - Clears active flag if leaving "Searched/Found"
   - Logs history entry
4. Frontend refreshes Kanban board

### Link Import Flow

1. User imports URLs via browser extension or file upload
2. Frontend sends POST request to `/api/links`
3. LinksController stores URLs in `job_links` table
4. Python scripts process URLs and populate `job_listings`
5. Frontend displays processed jobs

## Cross-Cutting Concerns

### CORS Configuration

- Default policy: Allow all origins, headers, methods
- Specific policy for Kuriāēpīai: `http://localhost:5173`
- Applied globally via `app.UseCors()`

### Error Handling

- Controllers return appropriate HTTP status codes
- BadRequest (400) for validation errors
- NotFound (404) for missing resources
- Unhandled exceptions return 500

### Security

- No authentication/authorization (single-user application)
- Parameterized SQL queries to prevent injection
- CORS configured for development
- HTTPS redirection enabled in production

### Logging

- Console logging for database path resolution
- Audit trail for string changes (if string management system is used)
- Kanban history tracks all status changes

## Deployment Architecture

### Development

- Backend: `dotnet run` on port 5300
- Frontend: `npm run dev` on port 5173
- Database: Local SQLite file in SearchCode/
- Hot reload enabled for both backend and frontend

### Production Considerations

- Frontend built with `npm run build`
- Backend published as self-contained executable
- Database file must be accessible to API process
- Static files can be served by API or separate web server
- CORS policies should be tightened
- HTTPS should be enforced

## Integration Points

### Python Scripts

- Direct SQLite database access
- Shared database file with C# API
- No API calls (direct DB operations)
- Used for bulk job scraping from Adzuna, Indeed, LinkedIn

### Browser Extension

- Captures job URLs from browser tabs
- Sends URLs to API via POST requests
- Uses offscreen document for URL extraction
- Background service worker for event handling

### Desktop Launcher

- .NET console application
- Launches web browser to application URL
- Simplifies application startup

## Scalability Considerations

### Current Limitations

- Single SQLite database file
- No concurrent write optimization
- No connection pooling
- All data in single file

### Potential Improvements

- Migrate to PostgreSQL or SQL Server for multi-user support
- Implement connection pooling
- Add caching layer (Redis)
- Separate read/write databases
- Implement API rate limiting
- Add authentication and authorization

## Monitoring & Observability

### Current State

- Console logging for startup
- Swagger UI for API exploration
- No structured logging
- No metrics collection

### Recommended Additions

- Structured logging (Serilog)
- Health check endpoints
- Application metrics (Prometheus)
- Error tracking (Sentry)
- Performance monitoring
- Database query logging
