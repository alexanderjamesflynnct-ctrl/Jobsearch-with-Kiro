# API Documentation

## Base URL

```
http://localhost:5300/api
```

## Authentication

No authentication required. This is a single-user application.

## CORS

The API allows requests from:

- Any origin (default policy)
- `http://localhost:5173` (Kuriāēpīai policy)

## Common Response Formats

### Success Response

```json
{
  "message": "Operation successful",
  "data": {}
}
```

### Error Response

```json
{
  "error": "Error description"
}
```

### Paginated Response

```json
{
  "total": 100,
  "page": 1,
  "per_page": 25,
  "jobs": []
}
```

---

## Jobs Controller

**Base Route**: `/api/jobs`

### Get Jobs

Retrieve job listings with filtering, sorting, and pagination.

**Endpoint**: `GET /api/jobs`

**Query Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `keywords` | string | No | Search in title, company, or description (LIKE search) |
| `country` | string | No | Filter by country (exact match) |
| `source` | string | No | Filter by source (exact match) |
| `job_type` | string | No | Filter by job type (LIKE search) |
| `is_remote` | string | No | Filter by remote status (exact match) |
| `state` | string | No | Filter by state (exact match) |
| `sort` | string | No | Sort column: `date_posted`, `title`, `company`, `country`, `salary`, `source`, `state`, `job_type` (default: `searched_at`) |
| `order` | string | No | Sort direction: `asc` or `desc` (default: `desc`) |
| `page` | int | No | Page number (default: 1) |
| `per_page` | int | No | Items per page (default: 25) |

**Response**: 200 OK

```json
{
  "total": 50,
  "page": 1,
  "per_page": 25,
  "jobs": [
    {
      "id": "abc123",
      "search_id": 1,
      "searched_at": "2024-01-15T10:30:00Z",
      "date_posted": "2024-01-14",
      "country": "United States",
      "title": "Software Engineer",
      "company": "Tech Corp",
      "location": "New York, NY",
      "city": "New York",
      "state": "NY",
      "job_type": "Full-time",
      "salary": "$100,000 - $150,000",
      "url": "https://example.com/job/123",
      "source": "indeed",
      "is_remote": "true",
      "description": "Job description..."
    }
  ]
}
```

### Delete Job

Delete a job listing and all associated Kanban data.

**Endpoint**: `DELETE /api/jobs/{id}`

**Parameters**:

- `id` (path): Job listing ID

**Response**: 200 OK

```json
{
  "message": "Deleted"
}
```

**Response**: 404 Not Found

### Update Job Country

Update the country field for a job listing.

**Endpoint**: `PATCH /api/jobs/{id}/country`

**Parameters**:

- `id` (path): Job listing ID

**Request Body**:

```json
{
  "country": "Canada"
}
```

**Response**: 200 OK

```json
{
  "message": "Updated"
}
```

**Response**: 400 Bad Request

```json
{
  "error": "country is required"
}
```

### Add Manual Job

Manually add a job listing to the database.

**Endpoint**: `POST /api/jobs/manual`

**Request Body**:

```json
{
  "title": "Software Engineer",
  "company": "Tech Corp",
  "location": "New York, NY",
  "city": "New York",
  "state": "NY",
  "country": "United States",
  "job_type": "Full-time",
  "salary": "$100,000 - $150,000",
  "url": "https://example.com/job/123",
  "source": "manual",
  "is_remote": "true",
  "description": "Job description..."
}
```

**Response**: 200 OK

```json
{
  "message": "Job added",
  "id": "manual_abc123def456"
}
```

**Response**: 400 Bad Request

```json
{
  "error": "Body required"
}
```

### Get Lookup Values

Get distinct values for filter dropdowns.

**Endpoints**:

- `GET /api/jobs/lookup/countries` - All unique countries
- `GET /api/jobs/lookup/sources` - All unique sources
- `GET /api/jobs/lookup/job-types` - All unique job types
- `GET /api/jobs/lookup/states` - All unique states

**Response**: 200 OK

```json
["United States", "Canada", "United Kingdom"]
```

### Delete Stale Jobs

Delete jobs that are in "Searched/Found" status with no history entries.

**Endpoint**: `DELETE /api/jobs/stale`

**Response**: 200 OK

```json
{
  "message": "Deleted 5 stale job(s)",
  "deleted": 5
}
```

---

## Kanban Controller

**Base Route**: `/api/kanban`

### Get Kanban Cards

Retrieve all Kanban cards with job details. Automatically syncs new jobs.

**Endpoint**: `GET /api/kanban`

**Response**: 200 OK

```json
[
  {
    "id": 1,
    "job_listing_id": "abc123",
    "status": "Searched/Found",
    "notes": "Looks promising",
    "is_active": 1,
    "fail_type": null,
    "updated_at": "2024-01-15T10:30:00Z",
    "title": "Software Engineer",
    "company": "Tech Corp",
    "location": "New York, NY",
    "state": "NY",
    "country": "United States",
    "salary": "$100,000 - $150,000",
    "job_type": "Full-time",
    "is_remote": "true",
    "source": "indeed",
    "url": "https://example.com/job/123",
    "date_posted": "2024-01-14"
  }
]
```

### Update Status

Update the status of a Kanban card and log the change.

**Endpoint**: `PATCH /api/kanban/{id}/status`

**Parameters**:

- `id` (path): Kanban job ID

**Request Body**:

```json
{
  "status": "Applied"
}
```

**Valid Statuses**:

- `Searched/Found`
- `Applied`
- `Interviewing`
- `Offered`
- `Rejected`

**Response**: 200 OK

```json
{
  "message": "Updated",
  "status": "Applied",
  "updated_at": "2024-01-15T10:30:00Z"
}
```

**Response**: 400 Bad Request

```json
{
  "error": "status required"
}
```

**Behavior**:

- Automatically logs status change to `kanban_history`
- Clears `is_active` flag when moving out of "Searched/Found"
- Updates `updated_at` timestamp

### Update Fail Type

Set a failure reason for a job application.

**Endpoint**: `PATCH /api/kanban/{id}/fail-type`

**Parameters**:

- `id` (path): Kanban job ID

**Request Body**:

```json
{
  "fail_type": "No response after 2 weeks"
}
```

**Response**: 200 OK

```json
{
  "message": "Updated"
}
```

### Toggle Active

Set a job as the active/focused job. Only one job can be active at a time.

**Endpoint**: `POST /api/kanban/{id}/active`

**Parameters**:

- `id` (path): Kanban job ID

**Response**: 200 OK

```json
{
  "message": "Set as active"
}
```

**Response**: 200 OK (if already active)

```json
{
  "message": "Deselected"
}
```

**Behavior**:

- Clears all other active flags
- Sets the specified job as active

### Get History

Retrieve status change history for a Kanban card.

**Endpoint**: `GET /api/kanban/{id}/history`

**Parameters**:

- `id` (path): Kanban job ID

**Response**: 200 OK

```json
[
  {
    "from_status": "Searched/Found",
    "to_status": "Applied",
    "changed_at": "2024-01-15T10:30:00Z"
  },
  {
    "from_status": "Applied",
    "to_status": "Interviewing",
    "changed_at": "2024-01-18T14:20:00Z"
  }
]
```

### Get Notes

Retrieve all notes for a Kanban card.

**Endpoint**: `GET /api/kanban/{id}/notes`

**Parameters**:

- `id` (path): Kanban job ID

**Response**: 200 OK

```json
[
  {
    "id": 1,
    "note": "Follow up with recruiter",
    "created_at": "2024-01-15T10:30:00Z"
  }
]
```

### Add Note

Add a note to a Kanban card.

**Endpoint**: `POST /api/kanban/{id}/notes`

**Parameters**:

- `id` (path): Kanban job ID

**Request Body**:

```json
{
  "note": "Follow up with recruiter"
}
```

**Response**: 201 Created

```json
{
  "message": "Note saved",
  "created_at": "2024-01-15T10:30:00Z"
}
```

**Response**: 400 Bad Request

```json
{
  "error": "note is required"
}
```

### Delete Note

Delete a note from a Kanban card.

**Endpoint**: `DELETE /api/kanban/notes/{noteId}`

**Parameters**:

- `noteId` (path): Note ID

**Response**: 200 OK

```json
{
  "message": "Deleted"
}
```

### Get Timers

Retrieve all time tracking entries for a Kanban card.

**Endpoint**: `GET /api/kanban/{id}/timers`

**Parameters**:

- `id` (path): Kanban job ID

**Response**: 200 OK

```json
[
  {
    "id": 1,
    "duration_seconds": 3600,
    "created_at": "2024-01-15T10:30:00Z"
  }
]
```

### Add Timer

Log time spent on a job application.

**Endpoint**: `POST /api/kanban/{id}/timer`

**Parameters**:

- `id` (path): Kanban job ID

**Request Body**:

```json
{
  "duration_seconds": 3600
}
```

**Response**: 201 Created

```json
{
  "message": "Timer saved",
  "duration_seconds": 3600
}
```

**Response**: 400 Bad Request

```json
{
  "error": "duration_seconds must be > 0"
}
```

---

## Prompts Controller

**Base Route**: `/api/prompts`

### Get Prompts

Retrieve all AI-assisted development prompts in sequence order.

**Endpoint**: `GET /api/prompts`

**Response**: 200 OK

```json
[
  {
    "sequence": 1,
    "date": "2024-01-15",
    "prompt": "Create a job search database",
    "category": "Database Design",
    "response": "Here's the SQL schema..."
  },
  {
    "sequence": 2,
    "date": "2024-01-15",
    "prompt": "Build the API controller",
    "category": "Backend",
    "response": "Here's the C# controller code..."
  }
]
```

---

## Settings Controller

**Base Route**: `/api/settings`

### Get Settings

Retrieve all application settings.

**Endpoint**: `GET /api/settings`

**Response**: 200 OK

```json
{
  "timezone": "America/New_York",
  "search_keywords": "Director of Software Engineering"
}
```

### Update Settings

Update multiple settings at once.

**Endpoint**: `PATCH /api/settings`

**Request Body**:

```json
{
  "timezone": "America/Chicago",
  "search_keywords": "VP of Engineering"
}
```

**Response**: 200 OK

```json
{
  "message": "Settings saved"
}
```

**Response**: 400 Bad Request

```json
{
  "error": "Settings data is required."
}
```

**Behavior**:

- Upserts each setting (insert or update)
- Uses SQL `ON CONFLICT` for atomic updates

---

## Stats Controller

**Base Route**: `/api/stats`

### Get Dashboard Stats

Retrieve summary statistics for the dashboard.

**Endpoint**: `GET /api/stats`

**Response**: 200 OK

```json
{
  "total_jobs": 150,
  "active_pipeline": 25,
  "applied": 45,
  "interviewing": 10,
  "offered": 3,
  "rejected": 67,
  "searched_found": 50,
  "countries": ["United States", "Canada", "United Kingdom"],
  "sources": ["indeed", "linkedin", "adzuna", "manual"]
}
```

---

## Links Controller

**Base Route**: `/api/links`

### Import Links

Import job URLs for processing.

**Endpoint**: `POST /api/links`

**Request Body**:

```json
{
  "urls": ["https://indeed.com/job/123", "https://linkedin.com/jobs/view/456"]
}
```

**Response**: 201 Created

```json
{
  "message": "Links imported",
  "count": 2
}
```

### Get Links

Retrieve imported links with processing status.

**Endpoint**: `GET /api/links`

**Query Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `processed` | bool | No | Filter by processed status |
| `source` | string | No | Filter by source |

**Response**: 200 OK

```json
[
  {
    "id": 1,
    "url": "https://indeed.com/job/123",
    "source": "indeed",
    "added_at": "2024-01-15T10:30:00Z",
    "processed": 1,
    "processed_at": "2024-01-15T10:35:00Z",
    "error_message": null
  }
]
```

### Delete Link

Delete an imported link.

**Endpoint**: `DELETE /api/links/{id}`

**Parameters**:

- `id` (path): Link ID

**Response**: 200 OK

```json
{
  "message": "Deleted"
}
```

---

## Useful Links Controller

**Base Route**: `/api/useful-links`

### Get Useful Links

Retrieve all saved useful links.

**Endpoint**: `GET /api/useful-links`

**Response**: 200 OK

```json
[
  {
    "id": 1,
    "description": "Resume Builder",
    "url": "https://example.com/resume-builder",
    "added_at": "2024-01-15T10:30:00Z"
  }
]
```

### Add Useful Link

Add a new useful link.

**Endpoint**: `POST /api/useful-links`

**Request Body**:

```json
{
  "description": "Resume Builder",
  "url": "https://example.com/resume-builder"
}
```

**Response**: 201 Created

```json
{
  "message": "Link added",
  "id": 1
}
```

### Delete Useful Link

Delete a useful link.

**Endpoint**: `DELETE /api/useful-links/{id}`

**Parameters**:

- `id` (path): Link ID

**Response**: 200 OK

```json
{
  "message": "Deleted"
}
```

---

## Adzuna Controller

**Base Route**: `/api/adzuna`

### Search Adzuna Jobs

Search for jobs using the Adzuna API.

**Endpoint**: `GET /api/adzuna/search`

**Query Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `keywords` | string | Yes | Job search keywords |
| `location` | string | No | Location/country |
| `page` | int | No | Page number (default: 1) |

**Response**: 200 OK

```json
{
  "results": [
    {
      "id": "adzuna_123",
      "title": "Software Engineer",
      "company": "Tech Corp",
      "location": "New York, NY",
      "salary": "$100,000 - $150,000",
      "url": "https://example.com/job/123",
      "description": "Job description..."
    }
  ],
  "total": 50
}
```

---

## Code Stats Controller

**Base Route**: `/api/code-stats`

### Get Code Statistics

Retrieve codebase statistics.

**Endpoint**: `GET /api/code-stats`

**Response**: 200 OK

```json
[
  {
    "id": 1,
    "file_type": ".cs",
    "file_count": 150,
    "line_count": 25000,
    "scanned_at": "2024-01-15T10:30:00Z"
  },
  {
    "id": 2,
    "file_type": ".jsx",
    "file_count": 45,
    "line_count": 8000,
    "scanned_at": "2024-01-15T10:30:00Z"
  }
]
```

### Scan Codebase

Trigger a new codebase scan.

**Endpoint**: `POST /api/code-stats/scan`

**Response**: 200 OK

```json
{
  "message": "Scan complete",
  "files_scanned": 195,
  "total_lines": 33000
}
```

---

## Kuria Controller

**Base Route**: `/api/kuria`

### Custom Endpoints

Custom endpoints for specific application features.

**Endpoint**: `GET /api/kuria/status`

**Response**: 200 OK

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "database": "connected"
}
```

---

## Error Codes

| Status Code | Description                                     |
| ----------- | ----------------------------------------------- |
| 200         | OK - Request successful                         |
| 201         | Created - Resource created successfully         |
| 400         | Bad Request - Invalid input or validation error |
| 404         | Not Found - Resource not found                  |
| 500         | Internal Server Error - Unexpected server error |

## Rate Limiting

No rate limiting is currently implemented. This is a single-user application.

## Versioning

The API is currently unversioned. Future versions will use URL versioning (e.g., `/api/v2/jobs`).

## Swagger UI

Interactive API documentation is available at:

```
http://localhost:5300/swagger
```

OpenAPI specification:

```
http://localhost:5300/openapi/v1.json
```
