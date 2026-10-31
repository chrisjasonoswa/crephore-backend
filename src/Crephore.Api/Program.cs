using Crephore.Api.Extensions;
using Crephore.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// services to the container.
builder.Services.AddApplicationServices(builder.Configuration);


var app = builder.Build();

// Seed a default super admin for local development (DefaultAdmin in appsettings.Development.json)
if (app.Environment.IsDevelopment())
{
    await app.SeedDefaultAdminAsync();
}

// Add Middleware for global exception handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(ProgramServiceExtensions.FrontendCorsPolicy);

// Uploaded photos, when they are stored on this machine (Storage:Provider "Local")
app.UseLocalFileStorage();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
