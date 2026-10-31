using Crephore.Api.Common.Authentication;
using Crephore.Api.Common.Storage;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Data.Seeders;
using Crephore.Api.Features.Auth;
using Crephore.Api.Features.EventGroups;
using Crephore.Api.Features.Events;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.Media;
using Crephore.Api.Features.Sites;
using Microsoft.AspNetCore.Identity;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Crephore.IntegrationTests.Infrastructure;


public abstract class BaseTestClass(DatabaseFixture fixture) : IAsyncLifetime
{
    protected readonly DatabaseFixture Fixture = fixture;

    // Same JSON settings as the API (camelCase, enums as strings), for reading and sending typed bodies.
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected IServiceScope Scope { get; private set; } = null!;
    protected AppDbContext Context { get; private set; } = null!;
    protected UserManager<AppUser> UserManager { get; private set; } = null!;

    // Factories
    protected AppUserFactory AppUserFactory { get; private set; } = null!;
    protected AppUserTokenFactory AppUserTokenFactory { get; private set; } = null!;
    protected EventFactory EventFactory { get; private set; } = null!;
    protected EventGroupFactory EventGroupFactory { get; private set; } = null!;
    protected EventUserFactory EventUserFactory { get; private set; } = null!;
    protected EventWebsiteFactory EventWebsiteFactory { get; private set; } = null!;

    // Services can be added here as needed
    // REMINDER: Also resolve services in InitializeAsync() below.
    protected IAuthService AuthService { get; private set; } = null!;
    protected ITokenProvider TokenProvider { get; private set; } = null!;
    protected IAdminSeeder AdminSeeder { get; private set; } = null!;
    protected IEventsService EventsService { get; private set; } = null!;
    protected IEventGroupsService EventGroupsService { get; private set; } = null!;
    protected IEventWebsitesService EventWebsitesService { get; private set; } = null!;
    protected IMediaService MediaService { get; private set; } = null!;
    protected ISitesService SitesService { get; private set; } = null!;
    protected IFileStorage FileStorage { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        await Fixture.ResetAsync();

        Scope = Fixture.Factory.Services.CreateScope();
        Context = Scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager = Scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Factories
        AppUserFactory = Scope.ServiceProvider.GetRequiredService<AppUserFactory>();
        AppUserTokenFactory = Scope.ServiceProvider.GetRequiredService<AppUserTokenFactory>();
        EventFactory = Scope.ServiceProvider.GetRequiredService<EventFactory>();
        EventGroupFactory = Scope.ServiceProvider.GetRequiredService<EventGroupFactory>();
        EventUserFactory = Scope.ServiceProvider.GetRequiredService<EventUserFactory>();
        EventWebsiteFactory = Scope.ServiceProvider.GetRequiredService<EventWebsiteFactory>();

        //Services, Providers
        AuthService = Scope.ServiceProvider.GetRequiredService<IAuthService>();
        TokenProvider = Scope.ServiceProvider.GetRequiredService<ITokenProvider>();
        AdminSeeder = Scope.ServiceProvider.GetRequiredService<IAdminSeeder>();
        EventsService = Scope.ServiceProvider.GetRequiredService<IEventsService>();
        EventGroupsService = Scope.ServiceProvider.GetRequiredService<IEventGroupsService>();
        EventWebsitesService = Scope.ServiceProvider.GetRequiredService<IEventWebsitesService>();
        MediaService = Scope.ServiceProvider.GetRequiredService<IMediaService>();
        SitesService = Scope.ServiceProvider.GetRequiredService<ISitesService>();
        FileStorage = Scope.ServiceProvider.GetRequiredService<IFileStorage>();
    }

    public virtual Task DisposeAsync()
    {
        Scope.Dispose();
        return Task.CompletedTask;
    }

    // Calls a service from several scopes (each with its own DbContext) at once, like simultaneous requests.
    // Returns each call's exception, or null when it succeeded.
    protected async Task<Exception?[]> RunConcurrentlyAsync<TService>(int times, Func<TService, Task> call)
        where TService : notnull
    {
        var scopes = Enumerable.Range(0, times).Select(_ => Fixture.Factory.Services.CreateScope()).ToList();
        try
        {
            return await Task.WhenAll(scopes.Select(async scope =>
            {
                try
                {
                    await call(scope.ServiceProvider.GetRequiredService<TService>());
                    return null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }));
        }
        finally
        {
            scopes.ForEach(scope => scope.Dispose());
        }
    }

    protected HttpClient CreateClient()
    {
        return Fixture.Factory.CreateClient();
    }

    // A client authenticated as a new user (signed in like a login), plus that user.
    protected async Task<(HttpClient Client, AppUser User)> CreateAuthenticatedClientAsync()
    {
        var user = await AppUserFactory.CreateAsync();
        var (accessToken, _) = await CreateAuthenticatedSessionAsync(user);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (client, user);
    }

    // Signs the user in like a login: a refresh token row plus an access token for the same session.
    protected async Task<(string AccessToken, string RefreshToken)> CreateAuthenticatedSessionAsync(AppUser user)
    {
        var (token, refreshToken) = await AppUserTokenFactory.CreateAsync(user);
        var accessToken = TokenProvider.CreateToken(user, token.SessionId);
        return (accessToken, refreshToken);
    }
}
