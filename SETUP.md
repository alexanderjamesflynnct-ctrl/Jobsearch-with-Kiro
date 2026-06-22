# Setup Guide

## Prerequisites

### Required Software

- **.NET 10 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Node.js 18+** - [Download here](https://nodejs.org/)
- **Python 3.10+** - [Download here](https://www.python.org/downloads/)
- **SQLite** - Included with .NET (Microsoft.Data.Sqlite)
- **Git** - [Download here](https://git-scm.com/downloads/)

### Optional Software

- **Visual Studio 2022** or **Visual Studio Code** - For C# development
- **Chrome/Edge** - For browser extension
- **Docker** - For containerized deployment (optional)

## Installation Steps

### 1. Clone the Repository

```bash
git clone https://github.com/alexanderjamesflynnct-ctrl/Jobsearch-with-Kiro.git
cd Jobsearch-with-Kiro
```

### 2. Backend Setup (C# API)

```bash
cd webapp/api_cs

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build

# Run the API (starts on http://localhost:5300)
dotnet run
```

The API will:

- Initialize the SQLite database at `../SearchCode/jobs.db`
- Create all necessary tables if they don't exist
- Start listening on `http://localhost:5300`
- Enable Swagger UI at `http://localhost:5300/swagger`

### 3. Frontend Setup (React)

Open a new terminal window:

```bash
cd webapp/client

# Install dependencies
npm install

# Start the development server (starts on http://localhost:5173)
npm run dev
```

The frontend will:

- Start Vite dev server on `http://localhost:5173`
- Hot reload on file changes
- Proxy API requests to `http://localhost:5300`

### 4. Database Setup

The database is automatically created when the API starts. However, you can manually initialize it:

```bash
cd SearchCode

# The database file will be created at: jobs.db
# No manual setup required - the API handles schema creation
```

### 5. Python Scripts Setup (Optional)

For job scraping functionality:

```bash
cd SearchCode

# Install Python dependencies (if any)
pip install -r requirements.txt  # Create this file if needed

# Test database connection
python -c "import db; print('Database connection successful')"
```

### 6. Browser Extension Setup (Optional)

#### Chrome/Edge Installation

1. Open Chrome/Edge and navigate to `chrome://extensions/`
2. Enable "Developer mode" (toggle in top right)
3. Click "Load unpacked"
4. Select the `browser-extension` folder
5. The extension icon should appear in your toolbar

#### Configuration

The extension is pre-configured to send URLs to `http://localhost:5300`. Ensure the API is running before using the extension.

### 7. Desktop Launcher Setup (Optional)

```bash
cd launcher

# Build the launcher
dotnet build

# Run the launcher
dotnet run
```

The launcher will:

- Open the default browser to `http://localhost:5173`
- Provide a convenient way to start the application

## Configuration

### API Configuration

Edit `webapp/api_cs/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Edit `webapp/api_cs/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.Data.Sqlite": "Warning"
    }
  }
}
```

### Frontend Configuration

Edit `webapp/client/vite.config.js` if you need to change the dev server port or proxy settings:

```javascript
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:5300",
        changeOrigin: true,
      },
    },
  },
});
```

### Database Configuration

The database path is configured in `webapp/api_cs/Data/JobSearchDatabase.cs`:

```csharp
var dbPath = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SearchCode", "jobs.db"));
```

To change the database location, modify the path construction logic.

### Application Settings

Default settings are stored in the database. You can modify them via:

1. **API**: `PATCH /api/settings`
2. **Frontend**: Settings page
3. **Direct Database**: Update the `settings` table

Default settings:

- `timezone`: `America/New_York`
- `search_keywords`: `Director of Software Engineering`

## Verification

### Verify Backend

1. Start the API: `dotnet run` in `webapp/api_cs`
2. Open browser to `http://localhost:5300/swagger`
3. You should see the Swagger UI with all API endpoints
4. Test a simple endpoint: `GET /api/stats`

### Verify Frontend

1. Start the frontend: `npm run dev` in `webapp/client`
2. Open browser to `http://localhost:5173`
3. You should see the Job Search DB application
4. Navigate through the pages (Dashboard, Search, Kanban, etc.)

### Verify Database

```bash
cd SearchCode

# Open SQLite database
sqlite3 jobs.db

# List tables
.tables

# Check settings
SELECT * FROM settings;

# Exit
.quit
```

## Troubleshooting

### Port Already in Use

If port 5300 or 5173 is already in use:

**Backend**: Edit `webapp/api_cs/Program.cs`

```csharp
app.Run("http://localhost:5301"); // Change to 5301
```

**Frontend**: Edit `webapp/client/vite.config.js`

```javascript
server: {
  port: 5174, // Change to 5174
}
```

### Database Not Found

If the API can't find the database:

1. Check the console output for the database path
2. Ensure the `SearchCode` folder exists relative to the API runtime directory
3. Manually create the database file if needed (it will be auto-initialized)

### CORS Errors

If you see CORS errors in the browser console:

1. Ensure the API is running on port 5300
2. Check that CORS is configured in `Program.cs`
3. Verify the frontend is running on port 5173

### Python Scripts Fail

If Python scripts can't access the database:

1. Ensure the database file exists at `SearchCode/jobs.db`
2. Check file permissions
3. Verify Python can import the `db.py` module

### Browser Extension Not Working

If the extension doesn't capture URLs:

1. Ensure the API is running
2. Check the extension permissions in Chrome/Edge
3. Open the extension's background page console for errors
4. Verify the offscreen document is working

## Environment Variables

### Backend

No environment variables are required. Configuration is via:

- `appsettings.json`
- `appsettings.Development.json`
- Command-line arguments

### Frontend

No environment variables are required. Configuration is via:

- `vite.config.js`
- `.env` file (if needed)

### Python Scripts

No environment variables are required. Configuration is via:

- Direct code modification
- Database settings

## Data Migration

### Export Data

```bash
cd SearchCode

# Export to SQL
sqlite3 jobs.db .dump > backup.sql

# Export to CSV (example for job_listings)
sqlite3 -header -csv jobs.db "SELECT * FROM job_listings;" > jobs.csv
```

### Import Data

```bash
cd SearchCode

# Import from SQL
sqlite3 jobs.db < backup.sql

# Import from CSV
sqlite3 jobs.db -cmd ".mode csv" -cmd ".import jobs.csv job_listings"
```

## Backup Strategy

### Automated Backup Script

Create `backup.sh` (Linux/Mac) or `backup.bat` (Windows):

```bash
#!/bin/bash
# backup.sh
DATE=$(date +%Y%m%d_%H%M%S)
cp SearchCode/jobs.db "backups/jobs_$DATE.db"
```

```batch
:: backup.bat
set DATE=%date:~10,4%%date:~4,2%%date:~7,2%_%time:~0,2%%time:~3,2%%time:~6,2%
copy SearchCode\jobs.db backups\jobs_%DATE%.db
```

### Schedule Backups

**Windows Task Scheduler**:

1. Open Task Scheduler
2. Create Basic Task
3. Trigger: Daily
4. Action: Start a program
5. Program: `backup.bat`

**Cron (Linux/Mac)**:

```bash
crontab -e
# Add: 0 0 * * * /path/to/backup.sh
```

## Production Deployment

### Build for Production

**Backend**:

```bash
cd webapp/api_cs
dotnet publish -c Release -o ./publish
```

**Frontend**:

```bash
cd webapp/client
npm run build
```

### Deploy Backend

```bash
cd webapp/api_cs/publish

# Run the published app
./JobApi
```

### Deploy Frontend

The built frontend files are in `webapp/client/dist/`. You can:

1. Serve them with the API (configure static files in `Program.cs`)
2. Deploy to a CDN (Netlify, Vercel, etc.)
3. Serve with Nginx/Apache

### Environment Considerations

- **Database**: Use a production-grade database (PostgreSQL, SQL Server)
- **HTTPS**: Enable HTTPS in production
- **CORS**: Restrict CORS to specific origins
- **Logging**: Add structured logging (Serilog)
- **Monitoring**: Add health checks and metrics
- **Backups**: Implement automated backups
- **Security**: Add authentication if multi-user support is needed

## Next Steps

After setup is complete:

1. Read [DEVELOPMENT.md](DEVELOPMENT.md) for development guidelines
2. Review [API.md](API.md) for API documentation
3. Explore [ARCHITECTURE.md](ARCHITECTURE.md) for system design details
4. Start adding job listings and building your pipeline!
