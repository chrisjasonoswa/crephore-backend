using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Storage;
using Crephore.Api.Common.Utilities;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Data.Seeders;
using Crephore.Api.Features.Auth;
using Crephore.Api.Features.EventGroups;
using Crephore.Api.Features.Events;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.Media;
using Crephore.Api.Features.Sites;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Crephore.Api.Extensions;

public static class ProgramServiceExtensions
{
    public const string FrontendCorsPolicy = "AllowFrontend";

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        AddApi(services);
        AddDatabase(services, configuration);
        AddAuthentication(services, configuration);
        AddCors(services, configuration);
        AddIdentity(services);
        AddDependencyInjections(services);
        AddFileStorage(services, configuration);
        AddModelValidation(services);
        return services;
    }


    private static void AddApi(IServiceCollection services)
    {
        // Add controllers and json options
        services.AddControllers(options =>
        {
            options.Conventions.Add(new RoutePrefixConvention(ApiRoutes.Prefix));
        }).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter()
            );
        });

        // Add API explorer for Swagger/OpenAPI
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Crephore API",
                Version = "v1"
            });

            options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", document)] = []
            });
        });
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var serverVersion = string.IsNullOrWhiteSpace(configuration["Database:ServerVersion"])
            ? ServerVersion.AutoDetect(connectionString)
            : ServerVersion.Parse(configuration["Database:ServerVersion"]!);

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(
                connectionString,
                serverVersion
            )
        );
    }


    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = configuration["JWT:Issuer"],

                        ValidateAudience = true,
                        ValidAudience = configuration["JWT:Audience"],

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(configuration["JWT:SigningKey"]!)
                        ),

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,

                        NameClaimType = ClaimTypes.NameIdentifier,
                        RoleClaimType = ClaimTypes.Role
                    };

                    options.Events = new JwtBearerEvents
                    {
                        // Whether to fetch token in the:
                        // 1. request header (for API clients)
                        // 2. cookies (for browser clients)
                        OnMessageReceived = context =>
                        {
                            // Prefer header first
                            var token = context.Request.Headers["Authorization"]
                                .FirstOrDefault()?.Split(" ").Last();

                            // then fallback to cookie
                            if (string.IsNullOrEmpty(token))
                            {
                                token = context.Request.Cookies[AuthCookies.AccessToken];
                            }

                            context.Token = token;

                            return Task.CompletedTask;
                        },

                        // Reject tokens whose session was logged out, or whose security stamp is outdated
                        // (password change/reset). Expired access tokens are renewed with POST auth/refresh.
                        OnTokenValidated = async context =>
                        {
                            if (context.Principal is null)
                            {
                                context.Fail("Missing principal.");
                                return;
                            }

                            var userId = context.Principal.GetUserId();
                            var sessionId = context.Principal.FindFirstValue(CustomClaimTypes.SessionId);
                            var tokenSecurityStamp = context.Principal.FindFirstValue(CustomClaimTypes.SecurityStamp);

                            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(tokenSecurityStamp))
                            {
                                context.Fail("Missing required token claims.");
                                return;
                            }

                            var validationService = context.HttpContext.RequestServices
                                .GetRequiredService<ITokenValidationService>();

                            var isValid = await validationService.IsTokenStateValidAsync(userId, sessionId, tokenSecurityStamp);

                            if (!isValid)
                            {
                                context.Fail("Token is no longer valid.");
                            }
                        }
                    };
                }
            );
        services.AddAuthorization();
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }


    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<AppUser>(options =>
        {
            // Matches the web sign-up form: at least 8 characters, no composition rules.
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 1;

            options.User.RequireUniqueEmail = true;

            // Brute-force protection for login
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<AppDbContext>()
        // Password reset and email confirmation tokens
        .AddDefaultTokenProviders();
    }


    private static void AddDependencyInjections(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ITokenProvider, TokenProvider>();
        services.AddScoped<ITokenValidationService, TokenValidationService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEventsService, EventsService>();
        services.AddScoped<IEventGroupsService, EventGroupsService>();
        services.AddScoped<IEventWebsitesService, EventWebsitesService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<ISitesService, SitesService>();

        services.AddScoped<IAdminSeeder, AdminSeeder>();
    }


    // Uploaded files: a local folder for now (Storage:Provider "Local"); Backblaze B2 (S3 API) comes with hosting
    private static void AddFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(StorageOptions.SectionName);
        services.Configure<StorageOptions>(section);

        var provider = section.Get<StorageOptions>()?.Provider ?? StorageOptions.LocalProvider;
        if (provider != StorageOptions.LocalProvider)
            throw new InvalidOperationException($"Storage provider \"{provider}\" is not supported yet; use \"{StorageOptions.LocalProvider}\".");

        services.AddSingleton<IFileStorage, LocalFileStorage>();
    }


    private static void AddModelValidation(IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => ModelValidationHelper.Execute(kvp.Key.Split('.').Last()),
                        kvp => kvp.Value!.Errors
                            .Select(e => ModelValidationHelper.FormatErrorMessage(e.ErrorMessage, kvp.Key.Split('.').Last()))
                            .ToList()
                    );

                var response = ApiResponse<object>.Fail(errors, "Some fields are invalid.");

                return new BadRequestObjectResult(response);
            };
        });
    }
}


public class RoutePrefixConvention(string prefix) : IControllerModelConvention
{
    private readonly AttributeRouteModel _routePrefix = new AttributeRouteModel(new RouteAttribute(prefix));

    public void Apply(ControllerModel controller)
    {
        foreach (var selector in controller.Selectors)
        {
            if (selector.AttributeRouteModel != null)
            {
                selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(_routePrefix, selector.AttributeRouteModel);
            }
            else
            {
                selector.AttributeRouteModel = _routePrefix;
            }
        }
    }
}
