using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Presidents.Application.Abstractions;
using Presidents.Application.Contracts;
using Presidents.Application.Rag;
using Presidents.Infrastructure.Identity;
using Presidents.Infrastructure.Ingestion;
using Presidents.Infrastructure.Persistence;

namespace Presidents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, bool useJwt)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IHistoryStore, HistoryStore>();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));
        services.AddOptions<AiOptions>().Bind(configuration.GetSection(AiOptions.SectionName));
        services.AddOptions<IngestionOptions>().Bind(configuration.GetSection(IngestionOptions.SectionName));

        if (useJwt)
        {
            var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
            if (string.IsNullOrWhiteSpace(jwt.Key))
            {
                if (!environment.IsDevelopment())
                    throw new InvalidOperationException("Defina Jwt__Key fora do ambiente de desenvolvimento.");
                jwt.Key = "DevelopmentOnly_NotForProduction_JwtKey_32!";
            }

            services.PostConfigure<JwtOptions>(options =>
            {
                if (string.IsNullOrWhiteSpace(options.Key) && environment.IsDevelopment())
                    options.Key = "DevelopmentOnly_NotForProduction_JwtKey_32!";
            });

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = string.IsNullOrWhiteSpace(jwt.Issuer) ? "HistoriaDosPresidentes" : jwt.Issuer,
                        ValidAudience = string.IsNullOrWhiteSpace(jwt.Audience) ? "HistoriaDosPresidentes" : jwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                        NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
                    };
                });
        }

        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
        else
            services.AddDistributedMemoryCache();

        services.AddHttpClient<ILlmClient, SelectingLlmClient>((provider, client) =>
        {
            var ai = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(ai.BaseUrl) ? "https://api.openai.com/v1/" : ai.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDataImporter, GovernmentLegislationImporter>();
        services.AddScoped<IDataImporter, NewsImporter>();
        services.AddScoped<IDataImporter, AcademicSourceImporter>();
        services.AddHttpContextAccessor();
        return services;
    }
}
