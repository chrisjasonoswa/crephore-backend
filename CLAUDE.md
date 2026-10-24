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
- Auth: JWT bearer (header or `accessToken` cookie) + server-side sessions (`app_user_sessions`)
- Identity: ASP.NET Core Identity via `IdentityUserContext<AppUser, int>` (users-only schema, no roles tables) and `UserManager<AppUser>` for all user, password, lockout, and security-stamp operations; never hand-roll hashing
- Tests: `xUnit` + `FluentAssertions` version 7 + `Respawn`, integration tests against a real MySQL database

Frontend: `../crephore-web` (Nuxt, `http://localhost:3000`). Keep request/response shapes compatible with it.

---

## Entity Description
- Naming: the user entity is `AppUser` (not `User`) to avoid clashing with `ControllerBase.User` (the current `ClaimsPrincipal`). Prefix user-related types the same way (`AppUserSession`, `AppUserDto`, `AppUserFactory`).
- AppUser (`IdentityUser<int>`): an account that signs up and logs in by email. Adds `FirstName`, `LastName`, `Timezone` (IANA id), `IsSuperAdmin`, and `CreatedAt`; `Email`, `PhoneNumber`, `PasswordHash`, `SecurityStamp`, and lockout fields come from Identity. Email is unique (unique index on `NormalizedEmail`). `UserName` is required by Identity but is generated (`user_<guid>`) and never user-facing. `IsSuperAdmin` is a platform-level flag, never settable through public signup. The first super admin comes from the `DefaultAdmin` seeder (see "Seeding").
- AppUserSession: one per login. Every access token carries its `SessionId` (`sid`) and the user's `SecurityStamp` (`sst`). A token is only accepted while its session is not revoked or expired and the stamp still matches. Logout revokes only the current session; rotating the security stamp signs the user out everywhere.
- Event: (planned) `AppUser.Events`. Add the navigation once the `Event` entity exists.

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
- `Common/Api`: `ApiResponse<T>`, `ApiRoutes`
- `Common/Authentication`: `ICurrentUser`, `ITokenProvider`, `AuthCookies`
- `Common/Authorization`: `CustomClaimTypes`, `DefaultRoles`
- `Common/Exceptions`: `AppException` and its subclasses
- `Common/Utilities`
- `Data/AppDbContext.cs`, `Data/Entities`, `Data/IEntities`, `Data/Migrations`
- `Data/Seeders`: startup data seeders (`IAdminSeeder`), invoked from `Extensions/WebApplicationExtensions.cs`
- `Extensions/ProgramServiceExtensions.cs`: all service registration (`Program.cs` stays thin)
- `Middleware/ExceptionHandlingMiddleware.cs`

### Tests (`tests/Crephore.IntegrationTests`)
Mirror API features by layer:
- `Tests/<Feature>/Controllers/*`
- `Tests/<Feature>/Services/*`
- `Tests/<Feature>/Infrastructure/*` for pipeline-level behavior (e.g. JWT/session validation)

Under each folder, use one test class per endpoint or sub-feature (e.g. `AuthSignupTests`, `AuthLoginTests`, `AuthLogoutTests`).
The same class name may exist in both `Controllers` and `Services`; namespaces keep them apart.

Test infrastructure:
- `Fixtures/DatabaseFixture.cs`: creates a temporary MySQL database `crephore-mysql-test_<random>`, migrates, resets with Respawn between tests, and drops it on dispose
- `Fixtures/DatabaseCollection.cs`: one `TestCollections.<Feature>` constant and `[CollectionDefinition]` per feature
- `Infrastructure/BaseTestClass.cs`: exposes `Context`, `UserManager`, factories, services, `CreateClient()`, `CreateAuthenticatedSessionAsync(user)`
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
   - `NotFoundException` → 404
   - `UnexpectedException` → 500
7. Unhandled exceptions return a generic 500 message; never leak exception details to clients.

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
   - Non-identity entities use `[Table(...)]` (e.g. `[Table("app_user_sessions")]`).
   - Identity entities (`AppUser`, `IdentityUserClaim<int>`, `IdentityUserLogin<int>`, `IdentityUserToken<int>`) must be renamed with `ToTable(...)` in `OnModelCreating` after `base.OnModelCreating(builder)`. `[Table]` is ignored for them because Identity's fluent configuration overrides data annotations.
   - Users are queried through the inherited `Context.Users` DbSet.
2. Every string column that is indexed or bounded gets `[MaxLength]` (MySQL cannot index `longtext`).
3. Configure indexes, unique constraints, and relationships in `AppDbContext.OnModelCreating`.
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
9. Add indexes for common lookup paths (foreign keys, unique lookups such as `NormalizedEmail`, `SessionId`).
10. Use a transaction when multiple related writes must be atomic.

---

## Authentication and Current User
1. Endpoints: `POST api/auth/signup`, `POST api/auth/login`, `POST api/auth/logout` (`[Authorize]`), `GET api/auth/me` (`[Authorize]`, returns the current `AppUserDto`; the web app calls it on load to restore the signed-in user).
2. Login creates an `AppUserSession`, returns `{ accessToken, sessionId, accessTokenExpiresAt, user }`, and sets HttpOnly `accessToken` and `sessionId` cookies for the web client.
3. The JWT bearer handler reads the token from the `Authorization` header first, then the `accessToken` cookie, and in `OnTokenValidated` rejects tokens whose session is revoked, expired, or missing, or whose security stamp is outdated (`ISessionValidationService`).
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
5. Use `TokenProvider` (or `CreateAuthenticatedSessionAsync(user)`) for auth tokens in tests. A token needs an active `AppUserSession` to be accepted.
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
3. Use PascalCase descriptive names (e.g. `AddEvents`, `AddTimezoneToUsers`).

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
