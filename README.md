# Crephore Backend

ASP.NET Core Web API for Crephore. The web client lives in `../crephore-web`.

Conventions for contributors and coding agents are in [CLAUDE.md](CLAUDE.md).

## Prerequisites

- .NET 10 SDK
- `dotnet-ef` tool (`dotnet tool install --global dotnet-ef`)
- MySQL running locally

The development connection string is configured in `src/Crephore.Api/appsettings.Development.json`:

```text
Server=localhost;Database=crephore-mysql;User=root;Password=root
```

The integration test connection string is configured in `src/Crephore.Api/appsettings.LocalTest.json`:

```text
Server=localhost;User=root;Password=root
```

Integration tests create a temporary MySQL database named like `crephore-mysql-test_<random>`, run migrations, and drop it when the fixture is disposed. The configured MySQL user must be able to create and drop databases.

## Database Migrations

Create or update the local development database:

```bash
dotnet ef database update --project src/Crephore.Api/Crephore.Api.csproj
```

Do not write migrations by hand. Generate them with:

```bash
dotnet ef migrations add <MigrationName> --project src/Crephore.Api/Crephore.Api.csproj --output-dir Data/Migrations
```

## Run The API

```bash
dotnet run --project src/Crephore.Api/Crephore.Api.csproj
```

Default launch URLs from `src/Crephore.Api/Properties/launchSettings.json`:

```text
HTTP:  http://localhost:5180
HTTPS: https://localhost:7180
```

Swagger is enabled in the Development environment at `/swagger`.
Sample requests are in `src/Crephore.Api/Crephore.Api.http`.

### Default admin (Development)

On startup in Development the API creates a super admin from `DefaultAdmin` in `appsettings.Development.json` if it does not exist yet:

```text
Email:    admin@crephore.local
Password: Admin12345!
```

If that email already belongs to a user, the user is made a super admin and keeps their password. Run `dotnet ef database update` first; with pending migrations the seeding is skipped with a warning. Production seeding is not set up yet.

### Uploaded files (Development)

Photos are stored on this machine in `src/Crephore.Api/uploads/` (git-ignored) and served by the API at `http://localhost:5180/uploads/...`, configured by `Storage` in `appsettings.Development.json`. Hosted environments will use Backblaze B2 (S3-compatible) instead; that provider is not added yet.

## Endpoints

| Method | Route              | Auth     | Description                                                         |
| ------ | ------------------ | -------- | ------------------------------------------------------------------- |
| POST   | `/api/auth/signup` | None     | Create a user. Returns the user.                                    |
| POST   | `/api/auth/login`  | None     | Returns access (30 min) and refresh (7 days) tokens and sets HttpOnly `accessToken`/`refreshToken`/`sessionId` cookies. |
| POST   | `/api/auth/refresh` | None    | Rotates the refresh token (cookie or body) and returns and sets new tokens. |
| POST   | `/api/auth/logout` | Required | Signs out this device only: revokes its refresh tokens (its access token stops working) and clears the cookies. |
| GET    | `/api/auth/me`     | Required | Returns the signed-in user.                                         |
| GET    | `/api/events` | Required | Events the user has access to, paged (`page`, `pageSize`, `searchKey`, `sort`, `eventGroupId`). |
| GET    | `/api/events/{id}` | Required | One event with the user's access, their group, and the timeline. |
| POST   | `/api/events` | Required | Create an event (the creator gets `Owner` access), optionally in a group, with its timeline items. |
| DELETE | `/api/events/{id}` | Required | Delete an event (Owner only). |
| PUT    | `/api/events/{id}` | Required | Edit an event, including its timeline (Owner only). |
| GET    | `/api/event-groups` | Required | The user's event groups with their events, paged. |
| POST   | `/api/event-groups` | Required | Create a group. |
| PUT    | `/api/event-groups/{id}` | Required | Rename a group. |
| DELETE | `/api/event-groups/{id}` | Required | Delete a group; its events are ungrouped, not deleted. |
| POST   | `/api/event-groups/{id}/events/{eventId}` | Required | Add an event to the group (moves it from any other group). |
| DELETE | `/api/event-groups/{id}/events/{eventId}` | Required | Remove an event from the group. |
| GET    | `/api/events/{eventId}/website` | Required | The event's website (theme and sections in order); `data` is null until one is saved. |
| PUT    | `/api/events/{eventId}/website` | Required | Save the whole website: creates it, updates sections with an id, adds new ones, deletes the rest (Owner only). |
| DELETE | `/api/events/{eventId}/website` | Required | Delete the website and its sections (Owner only). |
| POST   | `/api/events/{eventId}/website/publish` | Required | Publish the saved website: guests see it as it is now (Owner only). |
| POST   | `/api/events/{eventId}/website/unpublish` | Required | Stop showing the website to guests; the saved website stays (Owner only). |
| POST   | `/api/events/{eventId}/media` | Required | Upload a website photo (`multipart/form-data`, `file`; JPG, PNG, WebP or GIF up to 10 MB). Returns its URL (Owner only). |
| GET    | `/api/sites/{subdomain}` | None | A published event website for guests (404 when unpublished or unknown). |

All responses use the `{ success, data, message, errors }` envelope.

## Build

```bash
dotnet build Crephore.slnx
```

## Run Tests

Run all integration tests:

```bash
dotnet test Crephore.slnx
```

Run only the Auth tests:

```bash
dotnet test Crephore.slnx --filter "FullyQualifiedName~Tests.Auth"
```

Run a single test class by name:

```bash
dotnet test Crephore.slnx --filter "FullyQualifiedName~Auth.Services.AuthLoginTests"
```
