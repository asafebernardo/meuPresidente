using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Presidents.Application.Security;

namespace Presidents.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        foreach (var role in AppRoles.All)
        {
            if (await roles.RoleExistsAsync(role))
                continue;
            var created = await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Seed:AdminEmail ou Seed:AdminPassword não definidos. Nenhum administrador foi criado.");
            return;
        }

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true
            };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        if (!await users.IsInRoleAsync(user, AppRoles.Admin))
            await users.AddToRoleAsync(user, AppRoles.Admin);
    }
}
