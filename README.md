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

## Endpoints

| Method | Route              | Auth     | Description                                                         |
| ------ | ------------------ | -------- | ------------------------------------------------------------------- |
| POST   | `/api/auth/signup` | None     | Create a user. Returns the user.                                    |
| POST   | `/api/auth/login`  | None     | Returns an access token and sets HttpOnly `accessToken`/`sessionId` cookies. |
| POST   | `/api/auth/logout` | Required | Revokes the current session and clears the cookies.                |
| GET    | `/api/auth/me`     | Required | Returns the signed-in user.                                         |

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
