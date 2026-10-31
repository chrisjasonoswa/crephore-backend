# Crephore Backend: Agent Instructions

This file is the source of truth for this repository's coding, testing, API, EF Core, auth, migration, and change-quality guidance.
Follow it for every change. When project guidance changes, update this file (do not duplicate it elsewhere; `AGENTS.md` points here).

The structure mirrors the Zao POS backend (`~/Projects/zao/zao-pos-backend`). When something is not covered here, follow how that project does it.

---

## Tech Stack and Baseline
- Target framework: `.NET 10`
- Language: `C# 14`
- API style: ASP.NET Core Web API (controllers) with feature-based folders
- Data access: `EF Core 9` + `MySQL` via `Pomelo.EntityFrameworkCore.MySql`
- Auth: as in zao-pos-backend: short-lived JWT access tokens (header or `accessToken` cookie, 30 minutes) + rotating refresh tokens (`app_user_tokens`, 7 days)
- Identity: ASP.NET Core Identity via `IdentityUserContext<AppUser, int>` (users-only schema, no roles tables) and `UserManager<AppUser>` for all user, password, lockout, and security-stamp operations; never hand-roll hashing
- Tests: `xUnit` + `FluentAssertions` version 7 + `Respawn`, integration tests against a real MySQL database

Frontend: `../crephore-web` (Nuxt, `http://localhost:3000`). Keep request/response shapes compatible with it.

---

## Entity Description
- Naming: the user entity is `AppUser` (not `User`) to avoid clashing with `ControllerBase.User` (the current `ClaimsPrincipal`). Prefix user-related types the same way (`AppUserToken`, `AppUserDto`, `AppUserFactory`).
- AppUser (`IdentityUser<int>`): an account that signs up and logs in by email. Adds `FirstName`, `LastName`, `IsSuperAdmin`, and `CreatedAt`; `Email`, `PhoneNumber`, `PasswordHash`, `SecurityStamp`, and lockout fields come from Identity. Email is unique (unique index on `NormalizedEmail`). `UserName` is required by Identity but is generated (`user_<guid>`) and never user-facing. `IsSuperAdmin` is a platform-level flag, never settable through public signup. The first super admin comes from the `DefaultAdmin` seeder (see "Seeding").
- AppUserToken (`app_user_tokens`): a refresh token, stored only as its SHA-256 `TokenHash`, with the user's `SecurityStamp` when issued, a 7-day `ExpiresAt` (`TokenProvider.RefreshTokenLifetime`), the login's `SessionId`, and the login's hard limit `SessionExpiresAt` (`SessionMaxLifetime`, 30 days after sign-in; `ExpiresAt` never goes past it). Each refresh revokes it and sets `ReplacedByTokenHash`. Identity's own token table is `app_user_identity_tokens` (unused) so this one can keep zao's name.
- Event: the actual event (e.g. a birthday). It has no owner column: users reach it through `EventUser`. Has a `Timezone` (the venue's IANA id, e.g. `"Asia/Manila"`); `StartsAt`/`EndsAt` are stored in UTC and shown in that timezone everywhere. Has a `Type` (`EventType` enum, stored and serialized as its name, e.g. `"Wedding"`), venue fields (`VenueLatitude`, `VenueLongitude`, `VenueGooglePlaceId` stay null until Google Places is integrated), `StartsAt`/`EndsAt`, and a globally unique lowercase `Subdomain` for its website.
- Event `Timeline`: the program as a JSON array of strings in order (`["Opening", "Dinner", "First dance"]`, MySQL `json` column, default `[]`). There is no timeline table; items are trimmed, non-empty, and at most 150 characters.
- EventUser (`event_users`, key `UserId` + `EventId`): a user's access to an event. `Access` is a JSON array of `EventAccess` names (MySQL `json` column, e.g. `["Owner"]`). Each value grants exactly one feature, except `Owner`, which grants everything; check with `access.Grants(required)` (`Common/Authorization/EventAccessExtensions`). Use access values, not roles: add one value per new feature instead of bundling. The creator gets `["Owner"]`. Also holds the user's own `EventGroupId` for the event. Deleted with its event or user.
- EventGroup: a user's own grouping of events (`OwnerId`), name unique per owner. Events join a group through the user's `EventUser.EventGroupId`, so each user groups a shared event independently. Deleting a group ungroups its events (`EventGroupId` set null); it never deletes them.
- EventWebsite (`event_websites`): an event's website, at most one per event (unique `EventId`, deleted with the event): its theme (`PrimaryColor`, `AccentColor`, optional `BackgroundColor` as `#RRGGBB`, `FontPairingKey`, `CornerStyle`) and its `Sections`. The subdomain stays on `Event`. Publishing stores a copy in `PublishedContent` (json `PublishedWebsiteDto`: the theme and the visible sections) with `PublishedAt`; guests see that copy, not the latest save. Both null while unpublished.
- WebsiteSection (`website_sections`): one section of a website, in page order (`SortOrder` from 0), deleted with it. Section types, layouts, options and content fields are defined in the web app's section registry, not here: `Type`, `Layout` and `Background` are the web app's keys (checked against `Features/EventWebsites/WebsiteKeys`), `Options` (string → string) and `Content` are stored as MySQL `json` without the API knowing their fields, plus `ContentVersion` (for the web app to upgrade old content), `IsVisible` (hidden in the editor's sense: never shown to guests) and `Visibility` (`SectionVisibility`, stored by name: `Always`, `BeforeEvent`, `AfterEvent`; when guests see it around the event's start). Every section is `Always` for now (the default when a request leaves it out); nothing applies the other values yet. Keys are stored, so never rename one.
- Media (`media`): a file uploaded for an event (deleted with it), e.g. a website photo. `Type` (`MediaType`) and `Purpose` (`MediaPurpose`) are stored as names; the file is in `IFileStorage` under `StorageKey` (generated, e.g. `events/12/<guid>.jpg`, never the uploader's file name). Its URL comes from the storage (`IFileStorage.GetUrl`), not a column. `UploaderUserId` is cleared when that user is deleted.
- Access scoping: events are reached only through the current user's `EventUser` row (`eu.UserId == currentUserId`); groups filter `OwnerId == currentUserId`. No row answers 404 (never 403), so the event's existence is not revealed; a row whose `Access` does not grant the action answers 403 (`ForbiddenException`).

---

## Project Structure Conventions

### API (`src/Crephore.Api`)
Use feature-based structure:
- `Features/<FeatureName>/`
  - `<FeatureName>Controller.cs`
  - `<FeatureName>Service.cs` (interface `I<FeatureName>Service` and implementation in the same file)
  - `<FeatureName>Mapper.cs` (optional; static extension methods such as `ToAppUserDto()`)
  - `Dtos/*.cs`
    - `<Entity>Dto.cs` for a shared response shape (e.g. `AppUserDto` in `Features/AppUsers/Dtos`)
      - If a DTO uses another entity, reuse that entity's existing `<Entity>Dto` from its feature.
    - `<Operation><FeatureName>Dto.cs` with an operation prefix (`Create`, `Update`, `Get`, `List`, `Signup`, `Login`) holding the request/response DTOs
      - Example: `SignupDto.cs` contains `SignupRequestDto`; `LoginDto.cs` contains `LoginRequestDto` and `LoginResponseDto`.

Cross-cutting code lives in:
- `Common/Api`: `ApiResponse<T>`, `ApiRoutes`, `ApiPaginationRequestDto` / `ApiPaginationResponseDto<T>`, `QuerySortExtensions.ApplySort`
- `Common/Constants`: enums shared by entities and DTOs (e.g. `EventType`, `EventAccess`)
- `Common/Authentication`: `ICurrentUser`, `ITokenProvider`, `AuthCookies`
- `Common/Authorization`: `CustomClaimTypes`, `DefaultRoles`, `EventAccessExtensions` (`Grants`, and `context.RequireEventAccessAsync(eventId, userId, required?)`: the user's `EventUser` row, 404 without one, 403 when it lacks `required`; use it in every event-scoped service)
- `Common/Storage`: `IFileStorage` (save, delete, public URL by key), `StorageOptions` (`Storage` section), `LocalFileStorage`
- `Common/Exceptions`: `AppException` and its subclasses
- `Common/Utilities`
- `Data/AppDbContext.cs`, `Data/Entities`, `Data/IEntities`, `Data/Migrations`
- `Data/UniqueIndexes.cs` (names of unique indexes mapped to field errors) and `Data/DbContextExtensions.cs` (`SaveChangesOrThrowIfDuplicateAsync`, `IsUniqueViolation`)
- `Data/Seeders`: startup data seeders (`IAdminSeeder`), invoked from `Extensions/WebApplicationExtensions.cs`
- `Extensions/ProgramServiceExtensions.cs`: all service registration (`Program.cs` stays thin)
- `Middleware/ExceptionHandlingMiddleware.cs`

### Tests (`tests/Crephore.IntegrationTests`)
Mirror API features by layer:
- `Tests/<Feature>/Controllers/*`
- `Tests/<Feature>/Services/*`
- `Tests/<Feature>/Infrastructure/*` for pipeline-level behavior (e.g. JWT validation)

Under each folder, use one test class per endpoint or sub-feature (e.g. `AuthSignupTests`, `AuthLoginTests`, `AuthLogoutTests`).
The same class name may exist in both `Controllers` and `Services`; namespaces keep them apart.

Test infrastructure:
- `Fixtures/DatabaseFixture.cs`: creates a temporary MySQL database `crephore-mysql-test_<random>`, migrates, resets with Respawn between tests, and drops it on dispose
- `Fixtures/DatabaseCollection.cs`: one `TestCollections.<Feature>` constant and `[CollectionDefinition]` per feature
- `Infrastructure/BaseTestClass.cs`: exposes `Context`, `UserManager`, factories, services, `CreateClient()`, `CreateAuthenticatedSessionAsync(user)` (access + refresh token), and `RunConcurrentlyAsync<TService>(times, call)` (calls a service from separate scopes at once, like simultaneous requests)
- `Infrastructure/FeatureBaseTestClasses.cs`: one `<Feature>BaseTestClass` per feature, bound to its collection
- `Factories/CustomWebApplicationFactory.cs`: swaps in the test database and registers entity factories
- `Factories/Entities/<Entity>Factory.cs`: test data factories

When adding a new feature, add all of: `TestCollections.<Feature>` + collection definition, `<Feature>BaseTestClass`, factory registration in `CustomWebApplicationFactory`, and properties and resolution in `BaseTestClass`.

---

## API Conventions
1. All controller responses return `ApiResponse<T>`.
   - `ApiResponse<T>.Ok(...)` for success.
   - `ApiResponse<T>.Fail(...)` for errors (normally produced by `ExceptionHandlingMiddleware`).
2. Never return raw entities, anonymous types, or `PasswordHash`. Map to DTOs.
3. Routes come from `ApiRoutes` constants. Controllers use `[Route(ApiRoutes.<Feature>.Route)]`; the `api` prefix is added by `RoutePrefixConvention`, so do not repeat it. Tests call `ApiRoutes.<...>.FullPath`.
4. Controller action names: `List`, `Get`, `Create`, `Update`, `Delete` (auth uses `Signup`, `Login`, `Logout`).
5. Validate input with data annotations on request DTOs; `[ApiController]` returns 400 automatically with the shape
   `{ success: false, message: "Some fields are invalid.", errors: { "<camelCaseField>": ["..."] } }`.
   - Custom `ErrorMessage`s should start with "The <field> ...". The validation helper lowercases the PascalCase field name inside messages.
6. Business-rule failures throw `AppException` subclasses from services; never build error responses in services:
   - `AppValidationException` → 400 (include an `errors` dictionary keyed by camelCase field when a field is at fault)
   - `UnauthorizedException` → 401
   - `ForbiddenException` → 403 (signed in and can see the resource, but lacks the access, e.g. a non-owner deleting an event)
   - `NotFoundException` → 404
   - `UnexpectedException` → 500
7. Unhandled exceptions return a generic 500 message; never leak exception details to clients.
8. List endpoints (as in zao):
   - Request DTO `List<Feature>RequestDto : ApiPaginationRequestDto` bound `[FromQuery]`: `page` (default 1), `pageSize` (default 10), `searchKey`, repeated `sort=field:asc|desc` entries, plus feature filters (e.g. `eventGroupId`). Document the searched and sortable fields on the DTO.
   - The service applies owner scoping, filters, then `ApplySort(request.Sort, fieldMap, defaultSort, defaultDescending, tieBreaker: x => (object)x.Id)`. The `fieldMap` keys are lowercase; unknown fields fall back to the default sort. The id tie-breaker keeps pages deterministic.
   - Count before paging (`CountAsync`), then `Skip(request.Skip).Take(request.PageSize)`, and return `ApiPaginationResponseDto<T>` (`items`, `page`, `pageSize`, `totalCount`, `hasNextPage`, `hasPreviousPage`).
9. Enums are serialized as strings (`JsonStringEnumConverter`) and stored as strings (`HasConversion<string>()`).

---

## Coding Conventions
1. Keep changes minimal and scoped to the requested behavior.
2. Prefer constructor injection (primary constructors) and depend on interfaces for custom services.
3. Register every new service in `ProgramServiceExtensions.AddDependencyInjections`.
4. Reuse existing DTOs and mappers (`ToAppUserDto`, etc.) instead of duplicating mapping logic.
5. Use file-scoped namespaces matching the folder path (`Crephore.Api.Features.Auth`).
6. Add comments when the "why" is not obvious; avoid restating the code.
7. Test method names: `Should..._When...` (service tests) or `<Action>_Should..._When...` (controller tests).

---

## Entities and EF Core
1. Tables are `snake_case_plural`.
   - Non-identity entities use `[Table(...)]` (e.g. `[Table("app_user_tokens")]`).
   - Identity entities (`AppUser`, `IdentityUserClaim<int>`, `IdentityUserLogin<int>`, `IdentityUserToken<int>`) must be renamed with `ToTable(...)` in `OnModelCreating` after `base.OnModelCreating(builder)`. `[Table]` is ignored for them because Identity's fluent configuration overrides data annotations.
   - Users are queried through the inherited `Context.Users` DbSet.
2. Every string column that is indexed or bounded gets `[MaxLength]` (MySQL cannot index `longtext`).
3. Configure indexes, unique constraints, and relationships in `AppDbContext.OnModelCreating`.
   - User-facing unique values (e.g. an event's subdomain, a group name per owner) are checked in the service first, so the error comes with any other field errors. Two simultaneous requests can both pass that check, so also save with `SaveChangesOrThrowIfDuplicateAsync(UniqueIndexes.<Name>, "<field>", "<message>")`: it turns that index's duplicate-key error into the same 400 field error instead of a 500. Add the index name to `UniqueIndexes` (EF's default `IX_<table>_<Columns>`, so no migration) and set it with `.HasDatabaseName(...)`. Cover it with a concurrent test using `RunConcurrentlyAsync` (see `CreateEventTests`).
4. Timestamps are set automatically in `AppDbContext.SaveChanges*`:
   - `IHasCreatedAt` → `CreatedAt` set on insert and never modified afterwards
   - `IHasTimestamp` (extends `IHasCreatedAt`) → also `UpdatedAt` on insert and update
   - Do not set these manually.

When adding or modifying queries:
1. Use `AsNoTracking()` for read-only queries.
2. Always apply a deterministic `OrderBy(...)` before `Skip/Take`.
3. Avoid N+1: use `Include`/`ThenInclude` or project to DTOs in one query.
4. For multiple collection includes, consider `AsSplitQuery()`.
5. Fetch only required columns where possible (project early).
6. Validate foreign IDs in batch (`Contains`) and compare counts.
7. `Distinct()` incoming ID lists before validation and updates.
8. Scope user-owned data by `UserId` on every relevant query.
9. Add indexes for common lookup paths (foreign keys, unique lookups such as `NormalizedEmail`, `TokenHash`).
10. Use a transaction when multiple related writes must be atomic.

---

## Events
1. Endpoints (all `[Authorize]`, scoped to the current user):
   - Reads need any access (an `EventUser` row); updating and deleting the event need `Owner` (give them their own `EventAccess` values once other roles exist). Grouping needs any access, since groups only organise the user's own list.
   - `GET api/events` (paged; `searchKey` matches name or subdomain; `eventGroupId` filter; sort `id`, `name`, `type`, `startsAt`, `createdAt`, `subdomain`, default `startsAt:asc`)
   - `GET api/events/{id}`, `POST api/events` (with optional `eventGroupId` and `timeline` string array, in program order), `DELETE api/events/{id}`
   - `PUT api/events/{id}`: same body and rules as create (`UpdateEventRequestDto : CreateEventRequestDto`); replaces every field, including the timeline and the user's own group. Its own subdomain does not count as taken.
   - `GET api/event-groups` (paged; sort `id`, `name`, `eventCount`, `createdAt`, default `name:asc`), `POST`, `PUT api/event-groups/{id}` (rename), `DELETE api/event-groups/{id}` (ungroups its events)
   - Create and update take an optional `eventIds`: the group then holds exactly those events (the user's own; they move out of any other group, the group's others are ungrouped). On update, leaving `eventIds` out keeps the current events.
   - `POST api/event-groups/{id}/events/{eventId}` (adds, moving it out of any other group), `DELETE api/event-groups/{id}/events/{eventId}`
2. Times: requests send wall-clock times without an offset (`"2027-06-12T15:00"`, bound to `DateTime`) plus the event's IANA `timezone`; `TimeZones.TryFind` / `TimeZones.ToUtc` (`Common/Utilities`) convert them to UTC. A time with an offset, a non-IANA timezone (including Windows ids), or a time skipped by a daylight-saving change is a field error. Responses return UTC plus `timezone`.
3. Create-event validation errors are keyed by field: `subdomain` (format `^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$` after trim + lowercase, and uniqueness), `endsAt` (not before `startsAt`), `timeline` (an empty or over-long item), `eventGroupId` (not the user's group).

---

## Event Websites and Media
1. Endpoints (all `[Authorize]`, through the current user's access to the event; reading needs any access, changing needs `Owner`):
   - `GET api/events/{eventId}/website`: the website, or `data: null` when the host hasn't saved one (not a 404, so the web app can tell "none yet" from an error)
   - `PUT api/events/{eventId}/website`: saves the whole website (`UpdateEventWebsiteRequestDto`: `theme` + `sections` in page order, at most 50). Creates it on the first save; sections with an `id` (of this website) are updated, sections without one added, saved ones left out deleted. Concurrent first saves never make two websites (unique `EventId`, `SaveChangesOrThrowIfDuplicateAsync`).
   - `DELETE api/events/{eventId}/website`: removes the website and its sections (the host starts over); 404 when there is none. Uploaded photos stay in the event's media.
   - `POST api/events/{eventId}/website/publish`: copies the saved website (theme and visible sections) into `PublishedContent`, so guests see it as it is now; 404 when never saved, a `sections` error when no section is visible. Unsaved edits in the editor are the web app's to save first.
   - `POST api/events/{eventId}/website/unpublish`: guests no longer see it; the saved website stays.
   - `EventWebsiteDto` has `publishedAt` and `hasUnpublishedChanges`: whether publishing now would change what guests see (the saved website's published form compared with `PublishedContent` as JSON values, since MySQL reorders stored JSON keys; hidden sections don't count).
   - `POST api/events/{eventId}/media`: `multipart/form-data` with the photo in `file`; returns the `MediaDto` with its `url`, which sections store in their content.
   - `GET api/sites/{subdomain}` (`[AllowAnonymous]`, `Features/Sites`): a published website for guests (`SiteDto`: the event's name, live `startsAt` and `timezone`, `publishedAt`, and the published theme and sections). Unpublished and unknown addresses are the same 404, so an unpublished website's existence is not revealed. The web app calls it while rendering on its server, so it needs no CORS for the guests' hosts.
2. Saving checks keys, not fields: theme `fontPairingKey` / `cornerStyle` and each section's `type`, `layout` (of that type) and `background` must be in `WebsiteKeys`; `content` must be a JSON object of at most 64 KB; `options` at most 20 entries of 64 characters. Section errors are keyed `sections` ("Section 2 has an unknown layout."), and the first error is also the message, because the editor shows it as a toast. When the web app adds a section type, layout, background, font pairing or corner style, add its key to `WebsiteKeys`.
3. The web app renders whatever is stored, so it makes it safe there: section links only render `#…`, `http(s):`, `mailto:` and `tel:` (`safeHref`).
4. Photos: up to 10 MB (`MediaService.PhotoMaxBytes`; requests up to 30 MB reach the service so larger photos get that message). The format is read from the file's first bytes (JPEG, PNG, WebP, GIF), never from its name or content type; anything else (including SVG, which can run scripts) is a `file` field error. The file is saved first, then the row; if the row fails, the file is deleted.
5. Storage (`Storage` section, `IFileStorage`):
   - `Local` (development): files in `Storage:Local:RootPath` (`uploads/` under the API project, git-ignored), served by the API at `RequestPath` (`/uploads`, static files with `nosniff` and long caching; `UseLocalFileStorage` in `Program.cs`); URLs are `PublicBaseUrl` + key (`http://localhost:5180/uploads/...`).
   - Hosted environments will use Backblaze B2 through its S3-compatible API (AWS SDK): add an `S3FileStorage : IFileStorage` and select it with `Storage:Provider`. Until then any other provider fails at startup.
   - Tests use `Local` in a temporary folder per test database (`CustomWebApplicationFactory.StorageRootPath`), deleted with the factory.
6. Not handled yet: files of deleted events, websites or replaced photos stay in storage (the `media` rows go with the event); clean them up with a background job later.

---

## Authentication and Current User
1. Endpoints: `POST api/auth/signup`, `POST api/auth/login`, `POST api/auth/refresh`, `POST api/auth/logout` (`[Authorize]`), `GET api/auth/me` (`[Authorize]`, returns the current `AppUserDto`; the web app calls it on load to restore the signed-in user).
2. Tokens follow zao-pos-backend:
   - Login returns `{ accessToken, refreshToken, sessionId, accessTokenExpiresAt, refreshTokenExpiresAt, user }`, stores the refresh token's hash (`AppUserToken`), and sets HttpOnly cookies: `accessToken` (until the access token expires), `sessionId`, and `refreshToken` with `Path=/api/auth/refresh` so it is only sent to the refresh endpoint (both until the refresh token expires). Access tokens last `JWT:ExpirationInMinutes` (30).
   - Refresh reads the refresh token from the `refreshToken` cookie (web) or the body (`RefreshRequestDto`, API clients), rotates it, and returns and sets new tokens for the same session, never past `SessionExpiresAt`. Unknown, expired, revoked tokens and a changed security stamp answer 401 "Your session has expired." (the reason is in `errors.refreshToken` and the logs).
   - Reuse detection: a replaced token sent again within `RefreshTokenReuseGracePeriod` (10 seconds, e.g. two tabs refreshing at once) gets new tokens for the same session, if the session is still active. Sent later, it is treated as stolen: every token of that session is revoked (`token_reused`), so whoever holds it must sign in again.
   - Logout signs out the current device only (unlike zao): it revokes that session's refresh tokens and leaves the security stamp alone, so other devices stay signed in. Rotate the stamp (`UpdateSecurityStampAsync`) to sign out everywhere.
3. The JWT bearer handler reads the token from the `Authorization` header first, then the `accessToken` cookie, and in `OnTokenValidated` (`ITokenValidationService`) rejects tokens whose session (`sid`) has no active refresh token (logged out on that device) or whose security stamp is outdated. This makes logout end that device's access token immediately, not after it expires.
4. Use `ICurrentUser` in controllers to read the current `userId`, `sessionId`, and `IsSuperAdmin()`. Pass these values into services as parameters; services should not read `HttpContext` claims.
5. Super admin endpoints use `[Authorize(Roles = DefaultRoles.SuperAdmin)]`.
6. Use `UserManager<AppUser>` for user operations:
   - `CreateAsync(user, password)` to create users; map `IdentityError`s to field errors with `MapIdentityErrorToField`.
   - `FindByEmailAsync` for email lookups (case-insensitive through `NormalizedEmail`); trim input emails first.
   - `CheckPasswordAsync` + `AccessFailedAsync` / `ResetAccessFailedCountAsync` + `IsLockedOutAsync` for login (lockout: 5 failed attempts → 15 minutes).
   - `UpdateSecurityStampAsync` after a password change/reset or any "sign out everywhere" action.
   - `GeneratePasswordResetTokenAsync` / `GenerateEmailConfirmationTokenAsync` (default token providers are registered) for reset and confirmation flows.
7. Password policy lives in `ProgramServiceExtensions.AddIdentity` (min 8 chars, matching the web sign-up form). Keep request DTO validation in sync with it.
8. Login failures return the same `"Invalid credentials"` message whether the email exists or not; only a locked-out account gets a distinct message.
9. For protected endpoints, add controller tests for at least: no token (`401`), insufficient role (`403`) when applicable, and the success status.

---

## Testing Conventions
1. Always use entity factories to create test data. If one is missing, create `<Entity>Factory` under `Factories/Entities` and register it. `AppUserFactory` creates users through `UserManager` with `AppUserFactory.GetValidPassword`.
2. Order tests in a class with success scenarios first, then failure and edge cases.
3. Service tests focus on the service's behavior:
   - Do not call other services for setup; use factories (or `Context` directly for edge cases).
   - Verify persistence through `Context` (use `AsNoTracking()` to read fresh state).
4. Controller tests focus on the HTTP layer only:
   - authorization (`401`/`403`)
   - model binding and validation (`400`)
   - one representative status code for the endpoint's outcome, plus response shape, status, and cookies
   - Do not re-test business rules already covered by service tests.
5. Use `TokenProvider` (or `CreateAuthenticatedSessionAsync(user)`, which also creates the refresh token row and returns both tokens) for auth tokens in tests; `AppUserTokenFactory` creates refresh tokens (returns the row and the raw token). `CreateAuthenticatedClientAsync()` returns a ready client plus its user.
   - Read and send typed bodies with `BaseTestClass.JsonOptions` (camelCase, enums as strings), like the API.
6. Do not override services per test with `WithWebHostBuilder` unless it is truly required.

---

## Seeding
1. `AdminSeeder` ensures a super admin exists. It reads the `DefaultAdmin` section (`Email`, `Password`, `FirstName`, `LastName`) and runs at startup **in Development only** (`Program.cs`).
2. It is idempotent: skipped when `Email`/`Password` are empty, promotes an existing user with that email without touching their password, otherwise creates the user through `UserManager`.
3. Dev credentials live in `appsettings.Development.json` (`admin@crephore.local` / `Admin12345!`). Never commit real credentials; production seeding is not set up yet.
4. `CustomWebApplicationFactory` blanks `DefaultAdmin` so test databases start empty. New seeders follow the same pattern: options class + `I<Name>Seeder`, never `HasData` for users.

---

## Migrations
1. Never write or edit migrations by hand.
2. Generate them with the EF CLI from the repository root:
   - `dotnet ef migrations add <MigrationName> --project src/Crephore.Api/Crephore.Api.csproj --output-dir Data/Migrations`
   - `dotnet ef database update --project src/Crephore.Api/Crephore.Api.csproj`
3. Use PascalCase descriptive names (e.g. `AddEvents`, `RemoveUserTimezone`).

---

## Commands
```bash
dotnet build Crephore.slnx
dotnet test Crephore.slnx
dotnet test Crephore.slnx --filter "FullyQualifiedName~Tests.Auth"
dotnet run --project src/Crephore.Api/Crephore.Api.csproj
```
Integration tests need local MySQL (`root`/`root`, see `src/Crephore.Api/appsettings.LocalTest.json`) with permission to create and drop databases.

---

## Change Quality Checklist
Before finalizing changes:
1. `dotnet build Crephore.slnx` succeeds with no new warnings.
2. Relevant tests are added, run, and pass (`dotnet test Crephore.slnx`).
3. New tests follow the folder, collection, factory, and naming conventions above.
4. No duplicated setup logic when a factory or infrastructure helper already provides it.
5. API response and error message formats stay consistent (`ApiResponse<T>`).
6. Schema changes have a generated migration.
7. `README.md` and this file are updated when setup, conventions, or entities change.
