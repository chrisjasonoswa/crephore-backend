using Crephore.Api.Common.Authentication;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Data.Seeders;
using Crephore.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Crephore.IntegrationTests.Infrastructure;


public abstract class BaseTestClass(DatabaseFixture fixture) : IAsyncLifetime
{
    protected readonly DatabaseFixture Fixture = fixture;

    protected IServiceScope Scope { get; private set; } = null!;
    protected AppDbContext Context { get; private set; } = null!;
    protected UserManager<AppUser> UserManager { get; private set; } = null!;

    // Factories
    protected AppUserFactory AppUserFactory { get; private set; } = null!;
    protected AppUserSessionFactory AppUserSessionFactory { get; private set; } = null!;

    // Services can be added here as needed
    // REMINDER: Also resolve services in InitializeAsync() below.
    protected IAuthService AuthService { get; private set; } = null!;
    protected ITokenProvider TokenProvider { get; private set; } = null!;
    protected IAdminSeeder AdminSeeder { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        await Fixture.ResetAsync();

        Scope = Fixture.Factory.Services.CreateScope();
        Context = Scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager = Scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Factories
        AppUserFactory = Scope.ServiceProvider.GetRequiredService<AppUserFactory>();
        AppUserSessionFactory = Scope.ServiceProvider.GetRequiredService<AppUserSessionFactory>();

        //Services, Providers
        AuthService = Scope.ServiceProvider.GetRequiredService<IAuthService>();
        TokenProvider = Scope.ServiceProvider.GetRequiredService<ITokenProvider>();
        AdminSeeder = Scope.ServiceProvider.GetRequiredService<IAdminSeeder>();
    }

    public virtual Task DisposeAsync()
    {
        Scope.Dispose();
        return Task.CompletedTask;
    }

    protected HttpClient CreateClient()
    {
        return Fixture.Factory.CreateClient();
    }

    // Creates an active session for the user and returns a bearer token bound to it.
    protected async Task<(string AccessToken, AppUserSession Session)> CreateAuthenticatedSessionAsync(AppUser user)
    {
        var session = await AppUserSessionFactory.CreateAsync(user);
        var accessToken = TokenProvider.CreateToken(user, session.SessionId);
        return (accessToken, session);
    }
}
