# Job Search Database Application

A full-stack job search management system built with a C# ASP.NET Core Web API backend and a React frontend. The application helps users track job applications, manage a Kanban-style pipeline, import job listings from multiple sources, and log AI-assisted development prompts.

## Overview

This application was developed with AI assistance using Google AI Studio. It provides a centralized database for managing job search activities, including:

- **Job Search & Filtering**: Search and filter job listings by keywords, country, source, job type, and remote status
- **Kanban Pipeline**: Track job applications through stages (Searched/Found → Applied → Interviewing → Offered → Rejected)
- **Manual Job Entry**: Add jobs manually with full metadata
- **Link Import**: Import job URLs from browser extensions or bulk imports
- **Useful Links**: Store and organize helpful job search resources
- **AI Prompts Log**: Track AI-assisted development prompts and responses
- **Code Statistics**: Monitor codebase metrics and file statistics
- **Settings Management**: Configure timezone, search keywords, and application preferences

## Tech Stack

### Backend

- **Framework**: ASP.NET Core Web API (.NET 10)
- **Database**: SQLite (via Microsoft.Data.Sqlite)
- **API Documentation**: Swashbuckle.AspNetCore (Swagger) + .NET 10 Native OpenAPI
- **Architecture**: Controller-based REST API with ADO.NET data access

### Frontend

- **Framework**: React 18
- **Routing**: react-router-dom v7
- **Build Tool**: Vite
- **UI**: Custom CSS with Swagger UI integration
- **State Management**: React hooks with pub/sub pattern for string management

### Browser Extension

- Chrome/Edge browser extension for capturing job URLs
- Background service worker and offscreen document for URL extraction

### Desktop Launcher

- .NET console application for launching the web application

## Project Structure

```
jobsearch/
├── webapp/
│   ├── api_cs/                    # C# ASP.NET Core Web API
│   │   ├── Controllers/           # API controllers
│   │   │   ├── JobsController.cs
│   │   │   ├── KanbanController.cs
│   │   │   ├── PromptsController.cs
│   │   │   ├── SettingsController.cs
│   │   │   ├── StatsController.cs
│   │   │   ├── LinksController.cs
│   │   │   ├── UsefulLinksController.cs
│   │   │   ├── AdzunaController.cs
│   │   │   ├── CodeStatsController.cs
│   │   │   └── KuriaController.cs
│   │   ├── Data/
│   │   │   └── JobSearchDatabase.cs    # Database initialization and schema
│   │   ├── Program.cs                  # Application entry point
│   │   └── JobApi.csproj               # Project file
│   └── client/                    # React frontend
│       ├── src/
│       │   ├── pages/             # Page components
│       │   │   ├── Dashboard.jsx
│       │   │   ├── JobSearch.jsx
│       │   │   ├── Kanban.jsx
│       │   │   ├── AddJob.jsx
│       │   │   ├── ImportLinks.jsx
│       │   │   ├── UsefulLinks.jsx
│       │   │   ├── CodeMap.jsx
│       │   │   ├── PromptsLog.jsx
│       │   │   └── Settings.jsx
│       │   ├── components/        # Reusable components
│       │   │   ├── JobTable.jsx
│       │   │   ├── Filters.jsx
│       │   │   ├── StatsBar.jsx
│       │   │   ├── Bookmarklet.jsx
│       │   │   └── SwaggerDocs.jsx
│       │   ├── utils/             # Utility functions
│       │   │   ├── anonymize.js
│       │   │   └── timezone.js
│       │   ├── App.jsx            # Main app component
│       │   └── main.jsx           # Entry point
│       └── package.json
├── SearchCode/                    # Python scripts for job scraping
│   ├── adzuna_jobs.py
│   ├── indeed_jobs.py
│   ├── linkedin_jobs.py
│   ├── linkedin_import.py
│   ├── db.py
│   └── jobs.db                    # SQLite database (shared with API)
├── browser-extension/             # Chrome/Edge extension
│   ├── manifest.json
│   ├── background.js
│   ├── offscreen.html
│   ├── offscreen.js
│   └── icon.png / icon.svg
├── launcher/                      # .NET desktop launcher
│   └── Program.cs
├── app_metadata/
│   └── prompts.csv                # AI prompts metadata
├── StringHandling_documentation.txt  # String management system docs
└── systempath.txt                 # System PATH configuration
```

## Key Features

### 1. Job Management

- View all job listings in a searchable, filterable table
- Add jobs manually with comprehensive metadata
- Delete jobs with cascading cleanup of Kanban data
- Update job country information
- Track job sources (Adzuna, Indeed, LinkedIn, manual)

### 2. Kanban Pipeline

- Visual pipeline board for tracking application stages
- Status history tracking with timestamps
- Notes and timers for each job
- Active job highlighting
- Stale job detection and cleanup

### 3. Link Import System

- Browser extension for capturing job URLs
- Bulk import from CSV or text files
- Automatic deduplication
- Processing status tracking
- Error message logging

### 4. AI Prompts Log

- Sequential logging of AI-assisted development prompts
- Category-based organization
- Response tracking
- Integration with Google AI Studio workflow

### 5. Code Statistics

- File type counting
- Line count tracking
- Scan history
- Codebase metrics dashboard

### 6. Settings & Configuration

- Timezone configuration
- Default search keywords
- Anonymization toggle for sensitive data
- Application preferences

## Database Schema

The application uses a single SQLite database (`SearchCode/jobs.db`) shared between the Python scraping scripts and the C# API. Key tables include:

- `searches` - Search query history
- `job_listings` - Main job data
- `job_links` - Imported URLs awaiting processing
- `kanban_jobs` - Pipeline tracking
- `kanban_history` - Status change log
- `kanban_notes` - User notes on jobs
- `job_timers` - Time tracking per job
- `settings` - Application configuration
- `prompts_log` - AI prompt history
- `code_stats` - Codebase metrics
- `useful_links` - Saved resources

## Getting Started

See [SETUP.md](SETUP.md) for installation and configuration instructions.

## Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for development guidelines and workflows.

## API Documentation

See [API.md](API.md) for detailed API endpoint documentation.

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for system design and architecture details.

## License

This project was created as a personal job search management tool.
