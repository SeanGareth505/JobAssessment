using System.Text;
using Backend.Data;
using Backend.Middleware;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db", tags: ["ready"]);
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"]);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddSingleton<ISadcValidationService, SadcValidationService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddHostedService<Backend.Services.OutboxPublisherBackgroundService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest
        };
        return new BadRequestObjectResult(problemDetails);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var env = builder.Environment;
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "OrderManagement";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "OrderManagement";
if (string.IsNullOrEmpty(jwtKey))
{
    if (env.IsProduction())
        throw new InvalidOperationException("Jwt:Key must be set in production (e.g. via environment or secrets).");
    jwtKey = "OrderManagement-MockSecretKey-AtLeast16Chars";
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (env.IsDevelopment())
                {
                    var hasAuth = !string.IsNullOrEmpty(context.Request.Headers.Authorization);
                    context.HttpContext.RequestServices.GetService<ILogger<Program>>()?
                        .LogInformation("JWT OnMessageReceived: {Path} | Authorization header present: {HasAuth}", context.Request.Path, hasAuth);
                }
                return System.Threading.Tasks.Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                if (env.IsDevelopment())
                    context.HttpContext.RequestServices.GetService<ILogger<Program>>()?
                        .LogDebug(context.Exception, "JWT validation failed");
                return System.Threading.Tasks.Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Orders.Read", p => p.RequireRole("Orders.Read", "Orders.Write", "Orders.Admin"));
    options.AddPolicy("Orders.Write", p => p.RequireRole("Orders.Write", "Orders.Admin"));
    options.AddPolicy("Orders.Admin", p => p.RequireRole("Orders.Admin"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FullStack API v1");
    });
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (var i = 0; i < 30; i++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database not ready, retry {Attempt}/30 in 2s...", i + 1);
            await Task.Delay(2000);
        }
    }

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("SeedData:RunOnStartup"))
    {
        await SeedData.SeedIfEmptyAsync(db, logger);
    }
}

app.UseMiddleware<ExceptionProblemDetailsMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestMetricsMiddleware>();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/readiness", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.MapControllers();

app.Run();
