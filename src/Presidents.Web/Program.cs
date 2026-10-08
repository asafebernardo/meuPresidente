using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Presidents.Application;
using Presidents.Application.Contracts;
using Presidents.Application.Services;
using Presidents.Infrastructure;
using Presidents.Infrastructure.Identity;
using Presidents.Infrastructure.Persistence;
using Presidents.Web.Components;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/web-.log", rollingInterval: RollingInterval.Day));

var culture = new CultureInfo("pt-BR");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(culture);
    options.SupportedCultures = [culture];
    options.SupportedUICultures = [culture];
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment, useJwt: false);
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/admin/entrar";
    options.AccessDeniedPath = "/admin/entrar";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Editor", policy => policy.RequireRole("Admin", "Editor", "Reviewer"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(Ip(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(Ip(http), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));
});

var app = builder.Build();
app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseRequestLocalization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' https: data:; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com data:; script-src 'self' 'unsafe-inline'; base-uri 'self'; frame-ancestors 'none'";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapGet("/robots.txt", (IConfiguration configuration) =>
{
    var canonical = Canonical(configuration);
    var body = $"User-agent: *\nAllow: /\nDisallow: /admin\nDisallow: /conta\n\nSitemap: {canonical}/sitemap.xml\n";
    return Results.Text(body, "text/plain", Encoding.UTF8);
});

app.MapGet("/sitemap.xml", async (PresidentService presidents, QueryService queries, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var canonical = Canonical(configuration);
    var people = await presidents.ListPublicAsync(null, 1, 100, cancellationToken);
    var laws = await queries.LawsAsync(new LawQuery { Page = 1, PageSize = 100 }, cancellationToken);
    var events = await queries.EventsAsync(new EventQuery { Page = 1, PageSize = 100 }, cancellationToken);
    var categories = await queries.HomeAsync(cancellationToken);
    var builder = new StringBuilder();
    builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    builder.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
    void Add(string path) => builder.Append("<url><loc>").Append(Xml(canonical + path)).Append("</loc></url>");
    Add("/");
    Add("/presidentes");
    Add("/linha-do-tempo");
    Add("/categorias");
    Add("/leis");
    Add("/acontecimentos");
    Add("/comparar");
    Add("/sobre");
    Add("/perguntar");
    foreach (var president in people.Items) Add($"/presidentes/{president.Slug}");
    foreach (var law in laws.Items) Add($"/leis/{law.Slug}");
    foreach (var ev in events.Items) Add($"/acontecimentos/{ev.Slug}");
    foreach (var category in categories.Categories) Add($"/categorias/{category.Slug}");
    builder.Append("</urlset>");
    return Results.Text(builder.ToString(), "application/xml", Encoding.UTF8);
});

app.MapPost("/conta/entrar", async (HttpContext http, IAntiforgery antiforgery, SignInManager<AppUser> signIn) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync();
    var result = await signIn.PasswordSignInAsync(form["email"].ToString().Trim(), form["password"].ToString(), isPersistent: true, lockoutOnFailure: true);
    return Results.Redirect(result.Succeeded ? "/admin" : "/admin/entrar?erro=1");
}).AllowAnonymous().RequireRateLimiting("auth");

app.MapPost("/conta/sair", async (HttpContext http, IAntiforgery antiforgery, SignInManager<AppUser> signIn) =>
{
    await antiforgery.ValidateRequestAsync(http);
    await signIn.SignOutAsync();
    return Results.Redirect("/");
}).RequireAuthorization();

app.MapRazorComponents<App>();
await DatabaseInitializer.InitializeAsync(app.Services);
app.Run();

static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "local";
static string Canonical(IConfiguration configuration) => (configuration["Public:CanonicalBaseUrl"] ?? "http://localhost:5088").TrimEnd('/');
static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;
