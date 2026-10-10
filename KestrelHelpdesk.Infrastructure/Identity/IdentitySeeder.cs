using KestrelHelpdesk.Core.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KestrelHelpdesk.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services)
    {
        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole>>();

        var userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);
        await SeedUsersAsync(userManager);
    }

    private static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager)
    {
        var roles = new[]
        {
            AppRoles.Employee,
            AppRoles.Agent,
            AppRoles.Manager
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(
                    new IdentityRole(role));
            }
        }
    }

    private static async Task SeedUsersAsync(
        UserManager<ApplicationUser> userManager)
    {
        await CreateUserAsync(
            userManager,
            "employee@kestrelhelpdesk.com",
            "Employee123!",
            AppRoles.Employee);

        await CreateUserAsync(
            userManager,
            "agent@kestrelhelpdesk.com",
            "Agent123!",
            AppRoles.Agent);

        await CreateUserAsync(
            userManager,
            "manager@kestrelhelpdesk.com",
            "Manager123!",
            AppRoles.Manager);
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(
                user,
                password);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(e => e.Description));

                throw new InvalidOperationException(
                    $"Could not create user {email}: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}