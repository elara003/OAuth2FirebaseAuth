using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Data;

public static class SeedData
{
    public static async Task EnsureSeedData(UserManager<ApplicationUser> userManager)
    {
        // Create test user: alice
        var alice = await userManager.FindByNameAsync("alice");
        if (alice == null)
        {
            alice = new ApplicationUser
            {
                UserName = "alice",
                Email = "alice@example.com",
                EmailConfirmed = true,
                FirstName = "Alice",
                LastName = "Smith"
            };
            var result = await userManager.CreateAsync(alice, "Pass123!");
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
        }

        // Create test user: bob
        var bob = await userManager.FindByNameAsync("bob");
        if (bob == null)
        {
            bob = new ApplicationUser
            {
                UserName = "bob",
                Email = "bob@example.com",
                EmailConfirmed = true,
                FirstName = "Bob",
                LastName = "Johnson"
            };
            var result = await userManager.CreateAsync(bob, "Pass123!");
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
        }
    }
}
