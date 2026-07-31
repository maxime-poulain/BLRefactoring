using System.Text;
using BLRefactoring.DDD.Api.Authorization;
using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Application.Extensions;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Infrastructure.Extensions;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// The origins come from configuration rather than code, so a deployment names its own front
// end instead of requiring a rebuild. The policy used to allow any origin, which made its
// name a promise the code did not keep.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
    {
        // Deliberately no AllowCredentials: the JWT travels in the Authorization header, not in
        // a cookie, so credentials buy nothing here. Adding it would also make ASP.NET Core
        // reject any wildcard origin at startup, and the usual workaround for that error is a
        // policy that reflects the caller's origin back — which allows everyone while looking
        // restrictive.
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(options =>
{
    options.AddSecurity(JwtBearerDefaults.AuthenticationScheme, new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your token JWT"
    });

    options.OperationProcessors.Add(new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

builder.Services.AddTransient<ITrainingApplicationService, TrainingApplicationService>();
builder.Services.AddTransient<ITrainerApplicationService, TrainerApplicationService>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIntegrationEventHandlers();

builder.Services.AddDbContext<TrainingIdentityDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("TrainingContext"));
});

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Transient;
    options.Assemblies = [typeof(TrainerDeletedDomainEvent).Assembly, typeof(DeleteTrainingWhenTrainerDeletedEventHandler).Assembly];
});

builder.Services.AddIdentity<IdentityUser<Guid>, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 4;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;

        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<TrainingIdentityDbContext>()
    .AddDefaultTokenProviders();

// JWT
// The signing key has no safe fallback: failing at startup with a clear
// message beats a NullReferenceException on the first authenticated request.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Missing configuration value 'Jwt:Key'. Provide it via appsettings, " +
        "environment variables or user secrets before starting the host.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddScoped<IAuthorizationHandler, TrainingOwnerAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TrainingOwner", policy =>
        policy.Requirements.Add(new TrainingOwnerRequirement()));
});

var app = builder.Build();

// A warning rather than a throw, unlike Jwt:Key: this API is perfectly usable without a browser
// front end — Swagger is same-origin and the integration tests run in-process. But staying
// silent would leave a misconfiguration to surface as an opaque CORS error in a browser
// console, which is a miserable thing to debug.
if (allowedOrigins.Length == 0)
{
    app.Logger.LogWarning(
        "No 'Cors:AllowedOrigins' configured: every cross-origin browser request will be " +
        "refused. Set the key if a browser front end needs to call this API.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("BlazorClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
    await context.Database.MigrateAsync();
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TrainingIdentityDbContext>();
    await context.Database.MigrateAsync();
}

app.Run();

public partial class Program { }
