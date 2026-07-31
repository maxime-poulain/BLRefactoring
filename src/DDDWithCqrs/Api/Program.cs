using System.Text;
using BLRefactoring.DDDWithCqrs.Api.Authorization;
using BLRefactoring.DDDWithCqrs.Api.Middlewares;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Infrastructure.Extensions;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Shared with the layered host so the two cannot drift: a policy defined in one Program.cs only
// protects that host. See CorsExtensions for why the origins come from configuration.
builder.Services.AddApiCors(builder.Configuration);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();

builder.Services.AddMediator(configuration =>
{
    configuration.Assemblies =
    [
        typeof(CreateTrainerCommand).Assembly,      // DDDWithCqrs.Application: commands + command handlers
        typeof(TrainerCreatedDomainEvent).Assembly, // Shared.Domain: domain events
        typeof(MediatorCommandDispatcher).Assembly, // DDDWithCqrs.Infrastructure: query handlers
        typeof(SendWelcomeEmailWhenTrainerCreatedEventHandler).Assembly // Shared.Application: domain event handlers
    ];
    configuration.PipelineBehaviors = [typeof(ValidationPipelineBehavior<,>), typeof(NoTrackingDuringQueryExecutionBehavior<,>)];
    configuration.ServiceLifetime = ServiceLifetime.Transient;
});

builder.Services.AddTransient<ICommandDispatcher, MediatorCommandDispatcher>();
builder.Services.AddTransient<IQueryDispatcher, MediatorQueryDispatcher>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssembly(typeof(CreateTrainerCommandValidator).Assembly);

// Identity
builder.Services.AddDbContext<TrainingIdentityDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("TrainingContext"));
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

// Authorization
builder.Services.AddScoped<IAuthorizationHandler, TrainingOwnerAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TrainingOwner", policy =>
        policy.Requirements.Add(new TrainingOwnerRequirement()));
});

var app = builder.Build();

// Configure the HTTP request pipeline.

// The exception handlers come first, so that everything downstream is covered: authentication,
// authorization, routing and the actions alike. Registered after the authentication middlewares —
// where they used to sit — they let anything thrown while authenticating escape to the host.
// The global handler stays outside the validation one, which turns a ValidationException into a
// 400 before the global handler would turn it into a 500.
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<FluentValidationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Before authentication on purpose: an unauthenticated cross-origin call must still be answered
// with the CORS headers, or the browser reports a CORS failure instead of the 401 it received.
app.UseApiCors();

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
