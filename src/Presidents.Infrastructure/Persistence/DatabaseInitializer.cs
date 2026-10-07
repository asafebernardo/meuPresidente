using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Presidents.Infrastructure.Identity;

namespace Presidents.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")))
            throw new InvalidOperationException("Defina a variável ConnectionStrings__DefaultConnection.");

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (config.GetValue("Database:ApplyMigrations", environment.IsDevelopment()))
        {
            logger.LogInformation("Aplicando migrations do Entity Framework.");
            await db.Database.MigrateAsync(cancellationToken);
        }

        await IdentitySeeder.SeedAsync(scope.ServiceProvider, cancellationToken);
        if (config.GetValue("Seed:ApplyDemoData", environment.IsDevelopment()))
        {
            logger.LogInformation("Carregando dados de demonstração, quando ainda não existirem.");
            await DemoDataSeeder.SeedAsync(db, cancellationToken);
        }
    }
}
