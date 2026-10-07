using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Presidents.Api.Infrastructure;
using Presidents.Application;
using Presidents.Infrastructure;
using Presidents.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment, useJwt: true);
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddControllers().AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "História dos Presidentes",
            Version = "v1",
            Description = "API de pesquisa histórica. Respostas públicas exibem apenas conteúdo publicado e não classificam presidentes."
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "JWT no formato Bearer {token}",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Editor", policy => policy.RequireRole("Admin", "Editor", "Reviewer"));
        options.AddPolicy("Reviewer", policy => policy.RequireRole("Admin", "Reviewer"));
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    });
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("public", http => Partition(http, 120));
        options.AddPolicy("search", http => Partition(http, 30));
        options.AddPolicy("auth", http => Partition(http, 10));
    });
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5088"];
    builder.Services.AddCors(options => options.AddPolicy("web", policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        await next();
    });
    app.UseRateLimiter();
    app.UseCors("web");
    app.UseAuthentication();
    app.UseAuthorization();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "História dos Presidentes v1"));
    }

    app.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ok" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
    app.MapControllers();
    await DatabaseInitializer.InitializeAsync(app.Services);
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

static RateLimitPartition<string> Partition(HttpContext http, int permit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });

public partial class Program;
