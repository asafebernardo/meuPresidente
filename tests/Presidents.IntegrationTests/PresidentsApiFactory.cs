using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Presidents.IntegrationTests;

public sealed class PresidentsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ApiFactory.Connection);
        builder.UseSetting("Database:ApplyMigrations", "true");
        builder.UseSetting("Seed:ApplyDemoData", "true");
        builder.UseSetting("Seed:AdminEmail", ApiFactory.AdminEmail);
        builder.UseSetting("Seed:AdminPassword", ApiFactory.AdminPassword);
        builder.UseSetting("Jwt:Key", "DevelopmentOnly_NotForProduction_JwtKey_32!");
    }
}
