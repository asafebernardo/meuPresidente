using Npgsql;

namespace Presidents.IntegrationTests;

public sealed class ApiFactory : IAsyncLifetime
{
    public const string Database = "historia_presidentes_test";
    public const string Connection = $"Host=localhost;Port=5432;Database={Database};Username=postgres;Password=postgres";
    public const string AdminEmail = "admin@historiapresidentes.local";
    public const string AdminPassword = "ChangeMe_Dev_Only_123!";

    public PresidentsApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", Connection);
        Environment.SetEnvironmentVariable("Database__ApplyMigrations", "true");
        Environment.SetEnvironmentVariable("Seed__ApplyDemoData", "true");
        Environment.SetEnvironmentVariable("Seed__AdminEmail", AdminEmail);
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);
        Environment.SetEnvironmentVariable("Jwt__Key", "DevelopmentOnly_NotForProduction_JwtKey_32!");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        await using var admin = new NpgsqlConnection("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres");
        await admin.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()", admin))
        {
            terminate.Parameters.AddWithValue("name", Database);
            await terminate.ExecuteNonQueryAsync();
        }
        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {Database}", admin))
            await drop.ExecuteNonQueryAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {Database}", admin))
            await create.ExecuteNonQueryAsync();

        Factory = new PresidentsApiFactory();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
    }
}
