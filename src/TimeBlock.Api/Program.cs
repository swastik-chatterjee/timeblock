using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using TimeBlock.Api.Endpoints;
using TimeBlock.Api.Hubs;
using TimeBlock.Api.Services;
using TimeBlock.Core.Data;
using TimeBlock.Core.Services;

EnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var databaseProvider = builder.Configuration["Database:Provider"] ?? "Postgres";
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured. Set it in .env or the environment.");

builder.Services.AddDbContext<TimeBlockDbContext>(options =>
{
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString, sql => sql.MigrationsAssembly("TimeBlock.Api"));
    }
    else
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("TimeBlock.Api"));
    }
});

var tokenService = new TokenService(builder.Configuration);
builder.Services.AddSingleton(tokenService);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = tokenService.Issuer,
            ValidateAudience = true,
            ValidAudience = tokenService.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenService.SigningKey)),
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<TimeBlockStats>();
builder.Services.AddScoped<TimelineNotifier>();
builder.Services.AddSignalR();
if (builder.Configuration.GetValue("Worker:Enabled", true))
{
    builder.Services.AddHostedService<BlockWorker>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

app.UseBlazorFrameworkFiles();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<TimelineHub>("/hubs/timeline");
app.MapAuthEndpoints();
app.MapGroupEndpoints();
app.MapBlockEndpoints();
app.MapTemplateEndpoints();
app.MapNotificationEndpoints();
app.MapStatsEndpoints();
app.MapSettingsEndpoints();
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TimeBlockDbContext>();
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }
}

app.Run();

public partial class Program;
